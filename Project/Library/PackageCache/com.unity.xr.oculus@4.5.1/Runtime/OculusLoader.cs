﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Reflection;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR;

#if UNITY_INPUT_SYSTEM && ENABLE_VR
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using Unity.XR.Oculus.Input;
#endif

#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.MPE;
#endif


namespace Unity.XR.Oculus
{
#if UNITY_INPUT_SYSTEM && ENABLE_VR
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    static class InputLayoutLoader
    {
        static InputLayoutLoader()
        {
            RegisterInputLayouts();
        }

        public static void RegisterInputLayouts()
        {
            InputSystem.RegisterLayout<OculusHMD>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct("^(Oculus Rift)|^(Oculus Quest)|^(Oculus Go)"));
            InputSystem.RegisterLayout<OculusTouchController>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct(@"(^(Oculus Touch Controller))|(^(Oculus Quest Controller))"));
            InputSystem.RegisterLayout<OculusRemote>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct(@"Oculus Remote"));
            InputSystem.RegisterLayout<OculusTrackingReference>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct(@"((Tracking Reference)|(^(Oculus Rift [a-zA-Z0-9]* \(Camera)))"));
            InputSystem.RegisterLayout<OculusGoController>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct("^(Oculus Tracked Remote)"));

            InputSystem.RegisterLayout<OculusHMDExtended>();
            InputSystem.RegisterLayout<GearVRTrackedController>();
        }
    }
#endif

    public class OculusLoader : XRLoaderHelper
#if UNITY_EDITOR
    , IXRLoaderPreInit
#endif
    {
        private enum DeviceSupportedResult
        {
            Supported,             // we should attempt to initialize and run in VR
            NotSupported,          // we shouldn't attempt to initialize on this device, and should run in non-VR
            ExitApplication        // we're built for VR, but are on a non-Oculus Android device and should exit the application
        };

        static DeviceSupportedResult IsDeviceSupported()
        {
#if !ENABLE_VR
            return DeviceSupportedResult.NotSupported;
#elif UNITY_EDITOR_WIN
            if(RuntimePlatformChecks.IsSupportedPlatform())
                return DeviceSupportedResult.Supported;
            else
                return DeviceSupportedResult.NotSupported;
#elif (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            if(RuntimePlatformChecks.IsSupportedPlatform())
                return DeviceSupportedResult.Supported;
            else
                return DeviceSupportedResult.NotSupported;
#elif (UNITY_ANDROID && !UNITY_EDITOR)
            try
            {
                if (NativeMethods.GetIsSupportedDevice())
                    return DeviceSupportedResult.Supported;
                else
                    return DeviceSupportedResult.ExitApplication;
            }
            catch(DllNotFoundException)
            {
                // return NotSupported since we've been built with Oculus XR Plugin disabled
                return DeviceSupportedResult.NotSupported;
            }
#else
            return DeviceSupportedResult.NotSupported;
#endif
        }

#if ENABLE_VR
        private static List<XRDisplaySubsystemDescriptor> s_DisplaySubsystemDescriptors = new List<XRDisplaySubsystemDescriptor>();
        private static List<XRInputSubsystemDescriptor> s_InputSubsystemDescriptors = new List<XRInputSubsystemDescriptor>();

        public XRDisplaySubsystem displaySubsystem
        {
            get
            {
                return GetLoadedSubsystem<XRDisplaySubsystem>();
            }
        }

        public XRInputSubsystem inputSubsystem
        {
            get
            {
                return GetLoadedSubsystem<XRInputSubsystem>();
            }
        }
#endif

        private static NativeMethods.UnityVersion unityEngineVersion;

        public override bool Initialize()
        {
            if (!CheckUnityVersionCompatibility())
            {
                return false;
            }

            if (IsDeviceSupported() != DeviceSupportedResult.Supported
#if UNITY_EDITOR_WIN
                || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11
#endif
            )
            {
                return false;
            }

#if ENABLE_VR
#if UNITY_INPUT_SYSTEM
            InputLayoutLoader.RegisterInputLayouts();
#endif

            OculusSettings settings = GetSettings();

            if (settings != null)
            {
                NativeMethods.UserDefinedSettings userDefinedSettings;
                userDefinedSettings.sharedDepthBuffer = (ushort)(settings.SharedDepthBuffer ? 1 : 0);
                userDefinedSettings.dashSupport = (ushort)(settings.DashSupport ? 1 : 0);
                userDefinedSettings.stereoRenderingMode = (ushort)settings.GetStereoRenderingMode();
                userDefinedSettings.colorSpace = (ushort)((QualitySettings.activeColorSpace == ColorSpace.Linear) ? 1 : 0);
                userDefinedSettings.lowOverheadMode = (ushort)(settings.LowOverheadMode ? 1 : 0);
                userDefinedSettings.optimizeBufferDiscards = (ushort)(settings.OptimizeBufferDiscards ? 1 : 0);
                userDefinedSettings.symmetricProjection = (ushort)(settings.SymmetricProjection ? 1 : 0);
                userDefinedSettings.subsampledLayout = (ushort)(settings.SubsampledLayout ? 1 : 0);
                userDefinedSettings.foveatedRenderingMethod = (ushort)settings.FoveatedRenderingMethod;
                userDefinedSettings.lateLatching = (ushort)(settings.LateLatching ? 1 : 0);
                userDefinedSettings.lateLatchingDebug = (ushort)(settings.LateLatchingDebug ? 1 : 0);
                userDefinedSettings.enableTrackingOriginStageMode = (ushort)(settings.EnableTrackingOriginStageMode ? 1 : 0);
#if (UNITY_ANDROID && !UNITY_EDITOR)
                userDefinedSettings.spaceWarp = (ushort)(settings.SpaceWarp ? 1 : 0);
                userDefinedSettings.depthSubmission = (ushort)(settings.DepthSubmission ? 1 : 0);
                userDefinedSettings.optimizeMultiviewRenderRegions = (ushort)(settings.OptimizeMultiviewRenderRegions ? 1 : 0);
#else
                userDefinedSettings.spaceWarp = 0;
                userDefinedSettings.depthSubmission = 0;
                userDefinedSettings.optimizeMultiviewRenderRegions = 0;
#endif
                NativeMethods.SetUserDefinedSettings(userDefinedSettings, unityEngineVersion);
            }

            CreateSubsystem<XRDisplaySubsystemDescriptor, XRDisplaySubsystem>(s_DisplaySubsystemDescriptors, "oculus display");
            CreateSubsystem<XRInputSubsystemDescriptor, XRInputSubsystem>(s_InputSubsystemDescriptors, "oculus input");

            if (displaySubsystem == null && inputSubsystem == null)
            {
#if (UNITY_ANDROID && !UNITY_EDITOR)
                Debug.LogWarning("Unable to start Oculus XR Plugin.");
#else
                Debug.LogWarning("Unable to start Oculus XR Plugin.\n" +
                                 "Possible causes include a headset not being attached, or the Oculus runtime is not installed or up to date.\n" +
                                 "If you've recently installed or updated the Oculus runtime, you may need to reboot or close Unity and the Unity Hub and try again.");
#endif
            }
            else if (displaySubsystem == null)
            {
                Debug.LogError("Unable to start Oculus XR Plugin. Failed to load display subsystem.");
            }
            else if (inputSubsystem == null)
            {
                Debug.LogError("Unable to start Oculus XR Plugin. Failed to load input subsystem.");
            }
            else
            {
                RegisterUpdateCallback.Initialize();
            }


#if UNITY_2023_2_OR_NEWER && UNITY_URP_16
            if (settings != null && settings.FoveatedRenderingMethod == OculusSettings.FoveationMethod.EyeTrackedFoveatedRenderingUsingUnityAPIForURP)
            {
                // With Unity's generic foveation API, we will request eye tracking permission here to prevent needing code specific to a single plugin to control foveation
                Utils.eyeTrackedFoveatedRenderingEnabled = true;
            }
#endif

            return displaySubsystem != null && inputSubsystem != null;
#else
            return false;
#endif
        }

        public override bool Start()
        {
#if ENABLE_VR
            StartSubsystem<XRDisplaySubsystem>();
            StartSubsystem<XRInputSubsystem>();
#endif
            Development.OverrideDeveloperModeStart();

            return true;
        }

        public override bool Stop()
        {
#if ENABLE_VR
            StopSubsystem<XRDisplaySubsystem>();
            StopSubsystem<XRInputSubsystem>();
#endif
            Development.OverrideDeveloperModeStop();

            return true;
        }

        public override bool Deinitialize()
        {
            RegisterUpdateCallback.Deinitialize();

#if ENABLE_VR
            DestroySubsystem<XRDisplaySubsystem>();
            DestroySubsystem<XRInputSubsystem>();
#endif

            return true;
        }

#if UNITY_EDITOR_WIN
        [InitializeOnLoadMethod]
        static void EditorLoadOVRPlugin()
        {
            // early out for if this is invoked inside a secondary process (e.g. Standalone Profiler)
            if ((uint)ProcessService.level == (uint)ProcessLevel.Secondary)
                return;

            // early out for unsupported platforms like Windows Arm64
            if(!RuntimePlatformChecks.IsSupportedPlatform())
                return;

            string ovrpPath = "";

            // loop over all the native plugin importers and find the OVRPlugin the editor should use
            var importers = PluginImporter.GetAllImporters();
            foreach (var importer in importers)
            {
                if (!importer.GetCompatibleWithEditor() || !importer.assetPath.Contains("OVRPlugin"))
                    continue;

                if (!importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64) || !importer.assetPath.EndsWith(".dll"))
                    continue;

                ovrpPath = importer.assetPath;

                if (!importer.GetIsOverridable())
                    break;
            }

            if (ovrpPath == "")
            {
                Debug.LogError("Couldn't locate OVRPlugin.dll");
                return;
            }

            if (!NativeMethods.LoadOVRPlugin(AssetPathToAbsolutePath(ovrpPath)))
            {
                Debug.LogError("Failed to load OVRPlugin.dll");
                return;
            }
        }

        static string AssetPathToAbsolutePath(string assetPath)
        {
            var path = assetPath.Replace('/', Path.DirectorySeparatorChar);

            if (assetPath.StartsWith("Packages"))
            {
                path = String.Join(Path.DirectorySeparatorChar.ToString(), path.Split(Path.DirectorySeparatorChar).Skip(2));

                return Path.Combine(UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath).resolvedPath, path);
            }
            else
            {
                string assetsPath = Application.dataPath;
                assetsPath = assetsPath.Replace('/', Path.DirectorySeparatorChar);

                if (assetsPath.EndsWith("Assets"))
                {
                    assetsPath = assetsPath.Substring(0, assetsPath.LastIndexOf("Assets"));
                }

                return Path.Combine(assetsPath, assetPath);
            }
        }
#elif (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void RuntimeLoadOVRPlugin()
        {
            var supported = IsDeviceSupported();

            if (supported != DeviceSupportedResult.Supported)
                return;

            try
            {
                if (!NativeMethods.LoadOVRPlugin(""))
                    Debug.LogError("Failed to load OVRPlugin.dll");
            }
            catch
            {
                // handle Windows standalone build with Oculus XR Plugin installed but disabled in loader list.
            }
        }
#elif (UNITY_ANDROID && !UNITY_EDITOR)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void RuntimeLoadOVRPlugin()
        {
            var supported = IsDeviceSupported();

            if (supported == DeviceSupportedResult.ExitApplication)
            {
                Debug.LogError("\n\nExiting application:\n\nThis .apk was built with the Oculus XR Plugin loader enabled, but is attempting to run on a non-Oculus device.\nTo build for general Android devices, please disable the Oculus XR Plugin before building the Android player.\n\n\n");
                Application.Quit();
            }

            if (supported != DeviceSupportedResult.Supported)
                return;

            try
            {
                if (!NativeMethods.LoadOVRPlugin(""))
                    Debug.LogError("Failed to load libOVRPlugin.so");
            }
            catch
            {
                // handle Android standalone build with Oculus XR Plugin installed but disabled in loader list.
            }

        }
#endif

#if UNITY_EDITOR && XR_MGMT_GTE_320
#if UNITY_6000_1_OR_NEWER
        private void RemoveD3D12FromStandaloneGraphicsAPIs()
        {
            bool d3d12Removed = false;

            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64))
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                d3d12Removed = true;
            }

            GraphicsDeviceType[] oldApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            List<GraphicsDeviceType> newApisList = new List<GraphicsDeviceType>();

            // copy all entries except D3D12
            foreach (GraphicsDeviceType dev in oldApis)
            {
                if (dev == GraphicsDeviceType.Direct3D12)
                {
                    d3d12Removed = true;
                    continue;
                }

                newApisList.Add(dev);
            }

            // if we didn't remove D3D12 from the list, no need to do any further processing
            if (d3d12Removed == false)
                return;

            string removalWarning;

            if (newApisList.Count <= 0)
            {
                newApisList.Add(GraphicsDeviceType.Direct3D11);
                removalWarning = "Direct3D 12 is unsupported with the Oculus XR Plugin. It has been removed from your list of Windows graphics APIs and replaced with Direct3D 11.";
            }
            else
            {
                removalWarning = "Direct3D 12 is unsupported with the Oculus XR Plugin. It has been removed from your list of Windows graphics APIs.";
            }

            Debug.LogWarning(removalWarning);

            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, newApisList.ToArray());

            // restart the editor
            string dialogMsg = removalWarning + "\n\nRestart the editor now to use the updated settings, otherwise a restart will be required later to run on device.";
            if (EditorUtility.DisplayDialog("Unity editor restart required", dialogMsg, "Restart", "Continue"))
            {
                typeof(EditorApplication).GetMethod("RestartEditorAndRecompileScripts", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
            }
        }
#endif // UNITY_6000_1_OR_NEWER

        private void SetAndroidMinSdkVersion()
        {
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel23)
            {
                Debug.Log("The Android Minimum API Level has been updated to 23 in Player Settings as this is the minimum required for Oculus builds.");
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            }
        }

        public override void WasAssignedToBuildTarget(BuildTargetGroup buildTargetGroup)
        {
#if UNITY_6000_1_OR_NEWER
            if (buildTargetGroup == BuildTargetGroup.Standalone)
            {
                RemoveD3D12FromStandaloneGraphicsAPIs();
            }
#endif

            if (buildTargetGroup == BuildTargetGroup.Android)
            {
                SetAndroidMinSdkVersion();
            }
        }
#endif // UNITY_EDITOR && XR_MGMT_GTE_320

        public OculusSettings GetSettings()
        {
            OculusSettings settings = null;
#if UNITY_EDITOR
            UnityEditor.EditorBuildSettings.TryGetConfigObject<OculusSettings>("Unity.XR.Oculus.Settings", out settings);
#else
            settings = OculusSettings.s_Settings;
#endif
            return settings;
        }

#if UNITY_EDITOR
        public string GetPreInitLibraryName(BuildTarget buildTarget, BuildTargetGroup buildTargetGroup)
        {
            return "OculusXRPlugin";
        }
#endif

        private bool CheckUnityVersionCompatibility()
        {
#if !UNITY_2021_3_OR_NEWER
            Debug.LogWarning("This version of the Oculus XR Plugin requires at least Unity 2021.3.4f1.\n" +
                             "Please update to that version or higher of Unity, or use the verified Oculus XR Plugin package for this version of Unity.");
            return false;
#else
            // verify that 3.1.0+ versions of the package are on compatible versions of Unity
            var unityVersion = Application.unityVersion;
            var versionRegex = @"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?<type>.)(?<buildNumber>\d+)";
            var match = Regex.Match(unityVersion, versionRegex);

            // if we can't parse the Unity version number, just assume success
            if (!match.Success)
                return true;

            int major = Int32.Parse((match.Groups["major"].Value));
            int minor = Int32.Parse((match.Groups["minor"].Value));
            int patch = Int32.Parse((match.Groups["patch"].Value));
            char type = Char.Parse(match.Groups["type"].Value);
            int buildNumber = Int32.Parse((match.Groups["buildNumber"].Value));

            // 2021.3 requires at least 2021.3.4f1. this is handled by the package.json, but handle here for internal development as well
            if (major == 2021)
            {
                if ((minor < 3) || ((minor == 3) && (patch < 4)))
                {
                    Debug.LogWarning("This version of the Oculus XR Plugin requires at least Unity 2021.3.4f1.\n" +
                                     "Please update to that version or higher of Unity, or use the verified Oculus XR Plugin package for this version of Unity.");
                    return false;
                }
            }

            // 2022.1 requires at least 2022.1.12f1
            if ((major == 2022) && (minor == 1) && (patch < 12))
            {
                Debug.LogWarning("This version of the Oculus XR Plugin requires at least Unity 2022.1.12f1.\n" +
                                 "Please update to that version or higher of Unity, or use the verified Oculus XR Plugin package for this version of Unity.");
                return false;
            }

            // 2022.2 requires at least 2022.0b1
            if ((major == 2022) && (minor == 2) && (patch == 0) && (type == 'a'))
            {
                Debug.LogWarning("This version of the Oculus XR Plugin requires at least Unity 2022.2.0b1.\n" +
                                 "Please update to that version or higher of Unity, or use the verified Oculus XR Plugin package for this version of Unity.");
                return false;
            }

            unityEngineVersion.major = major;
            unityEngineVersion.minor = minor;
            unityEngineVersion.patch = patch;
            unityEngineVersion.type = type;
            unityEngineVersion.buildNumber = buildNumber;

            return true;
#endif
        }
    }
}
