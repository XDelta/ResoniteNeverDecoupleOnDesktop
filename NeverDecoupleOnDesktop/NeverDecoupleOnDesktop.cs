using FrooxEngine;
using HarmonyLib;
using Renderite.Shared;
using ResoniteModLoader;
using Elements.Core;

namespace NeverDecoupleOnDesktop;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class NeverDecoupleOnDesktop : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.1"; //Changing the version here updates it in all locations needed
	public override string Name => "NeverDecoupleOnDesktop";
	public override string Author => "Noble";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/noblereign/ResoniteNeverDecoupleOnDesktop/";

	public static ModConfiguration? Config;

	[AutoRegisterConfigKey]
	private static ModConfigurationKey<bool> Enabled = new ModConfigurationKey<bool>("Enabled", "Enables the mod, pretty self-explanatory.", () => true);

	public override void OnEngineInit() {
		Config = GetConfiguration();
		Config!.Save(true);

		Harmony harmony = new("dog.glacier.NeverDecoupleOnDesktop");
		harmony.PatchAll();

		Config.OnThisConfigurationChanged += OnModConfigChanged;

		Engine.Current.RunPostInit(() => {
			Msg($"Hooking to relevant events");
			Engine.Current.InputInterface.VRActiveChanged += VRActiveChanged;
		});
	}

	private static void OnModConfigChanged(ConfigurationChangedEvent configurationChangedEvent) {
		Msg($"Mod config changed, rerunning VRActiveChanged.");
		VRActiveChanged(Engine.Current.InputInterface.VR_Active);
	}

	private static void VRActiveChanged(bool active) {
		RenderDecouplingConfig config = new RenderDecouplingConfig();
		if (Engine.Current.RenderSystem != null && Userspace.UserspaceWorld != null) {
			RendererDecouplingSettings? currentSettings = Settings.GetActiveSetting<RendererDecouplingSettings>();
			bool ShouldApplyDesktopSettings = !active && Config!.GetValue(Enabled);
			Msg($"VRActiveChanged fired. Apply Desktop Settings: {ShouldApplyDesktopSettings}.");
			
			config.decoupleActivateInterval = ShouldApplyDesktopSettings ? float.PositiveInfinity : 1f / MathX.Max(0f, currentSettings?.ActivationFramerate.Value ?? 15f);
			config.recoupleFrameCount = ShouldApplyDesktopSettings ? 1 : (currentSettings?.DeactivationFrames.Value ?? 60);
			config.decoupledMaxAssetProcessingTime = (float)(currentSettings?.AssetProcessingMaxTimeMilliseconds.Value ?? 8f) * 0.001f;
			Userspace.UserspaceWorld.RunSynchronously(() => {
				Engine.Current.RenderSystem._messagingHost.SendCommand(config, isBackground: true);
			});
		} else {
			Warn($"VRActiveChanged fired, but missing: {(Engine.Current.RenderSystem != null ? "" : "RenderSystem")} ... {(Userspace.UserspaceWorld != null ? "" : "Userspace")}!");
		}
	}

	[HarmonyPatch(typeof(RenderSystem), "OnDecouplingSettingsChanged")]
	class RenderSystem_OnDecouplingSettingsChanged_Patch {
		static bool Prefix(RenderSystem __instance, RendererDecouplingSettings settings, ref RenderiteMessagingHost ____messagingHost) {
			if (Config!.GetValue(Enabled) && __instance != null && !__instance.Engine.InputInterface.VR_Active) {
				Msg($"Intercepted decoupling settings change event");
				RenderDecouplingConfig config = new RenderDecouplingConfig();
				config.decoupleActivateInterval = float.PositiveInfinity;
				config.recoupleFrameCount = 1;
				config.decoupledMaxAssetProcessingTime = (float)settings.AssetProcessingMaxTimeMilliseconds * 0.001f;
				____messagingHost.SendCommand(config, isBackground: true);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Userspace), "SetupUserspace")]
	class Userspace_SetupUserspace_Patch {
		static void Postfix(World __result) {
			__result.RunInUpdates(5, () => {
				Msg($"Init NeverDecoupleOnDesktop! VR active: {Engine.Current.InputInterface.VR_Active}");
				VRActiveChanged(Engine.Current.InputInterface.VR_Active);
			});
		}
	}
}
