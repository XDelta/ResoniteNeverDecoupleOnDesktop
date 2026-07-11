using FrooxEngine;
using HarmonyLib;
using Renderite.Shared;
using ResoniteModLoader;
using Elements.Core;

namespace NeverDecoupleOnDesktop;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class NeverDecoupleOnDesktop : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0"; //Changing the version here updates it in all locations needed
	public override string Name => "NeverDecoupleOnDesktop";
	public override string Author => "Noble";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/noblereign/NeverDecoupleOnDesktop/";

	public static ModConfiguration? Config;

	[AutoRegisterConfigKey]
	private static ModConfigurationKey<bool> Enabled = new ModConfigurationKey<bool>("Enabled", "Enables the mod, pretty self-explanatory.", () => true);

	public override void OnEngineInit() {
		Config = GetConfiguration();
		Config!.Save(true);

		Harmony harmony = new("dog.glacier.NeverDecoupleOnDesktop");
		harmony.PatchAll();

		Engine.Current.RunPostInit(() => {
			Engine.Current.InputInterface.VRActiveChanged += VRActiveChanged;
			VRActiveChanged(Engine.Current.InputInterface.VR_Active);
		});
	}
	private static void VRActiveChanged(bool active) {
		RenderDecouplingConfig config = new RenderDecouplingConfig();
		if (Engine.Current.RenderSystem != null) {
			RendererDecouplingSettings? currentSettings = Settings.GetActiveSetting<RendererDecouplingSettings>();
			bool ShouldApplyDesktopSettings = !active && Config!.GetValue(Enabled);
			Msg($"VRActiveChanged fired. Apply Desktop Settings: {ShouldApplyDesktopSettings}.");
			
			config.decoupleActivateInterval = ShouldApplyDesktopSettings ? float.PositiveInfinity : 1f / MathX.Max(0f, currentSettings?.ActivationFramerate.Value ?? 15f);
			config.recoupleFrameCount = ShouldApplyDesktopSettings ? 1 : (currentSettings?.DeactivationFrames.Value ?? 60);
			config.decoupledMaxAssetProcessingTime = (float)(currentSettings?.AssetProcessingMaxTimeMilliseconds.Value ?? 8f) * 0.001f;
			Engine.Current.RenderSystem._messagingHost.SendCommand(config, isBackground: true);
		}
	}

	[HarmonyPatch(typeof(RenderSystem), "OnDecouplingSettingsChanged")]
	class RenderSystem_OnDecouplingSettingsChanged_Patch {
		static bool Prefix(RenderSystem __instance, RendererDecouplingSettings settings, ref RenderiteMessagingHost ____messagingHost) {
			if (Config!.GetValue(Enabled) && __instance != null && !__instance.Engine.WorldManager.FocusedWorld.LocalUser.VR_Active) {
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
}
