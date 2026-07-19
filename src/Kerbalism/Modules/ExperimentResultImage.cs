using System;
using System.Collections;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;
using static KERBALISM.Experiment;

namespace KERBALISM
{
	/// <summary>
	/// Optional post-completion image viewer for a specific Kerbalism experiment.
	/// </summary>
	public class ExperimentResultImage : PartModule
	{
		[KSPField] public string experiment_id = string.Empty;
		// Scheme-less URLs are intentional: KSP config parsing treats '//' as a comment.
		[KSPField] public string image_url = string.Empty;
		[KSPField] public string image_backup = string.Empty;
		[KSPField] public string image_title = string.Empty;

		[KSPField(isPersistant = true)] public bool unlocked;
		[KSPField(isPersistant = true)] public bool observedProducing;

		private Experiment experiment;
		private Texture2D imageTexture;
		private PopupDialog imageDialog;
		private bool imageLoading;
		private bool ownsImageTexture;

		public override void OnStart(StartState state)
		{
			base.OnStart(state);

			Events["ViewImage"].guiActiveUncommand = true;
			Events["ViewImage"].externalToEVAOnly = true;
			Events["ViewImage"].requireFullControl = false;
			Events["ViewImage"].guiName = Local.Module_Experiment_ViewImage;
			Events["ViewImage"].active = false;

			FindExperiment();
		}

		public void Update()
		{
			if (!part.IsPAWVisible())
				return;

			Events["ViewImage"].active = Lib.IsFlight() && unlocked;
		}

		public void FixedUpdate()
		{
			if (!Lib.IsFlight())
				return;

			if (experiment == null)
				FindExperiment();

			if (experiment != null)
				ObserveStatus(experiment.Status, experiment.prodFactor > 0.0);
		}

		private void FindExperiment()
		{
			experiment = null;
			List<Experiment> experiments = part.FindModulesImplementing<Experiment>();
			for (int i = 0; i < experiments.Count; i++)
			{
				if (experiments[i].experiment_id == experiment_id)
				{
					experiment = experiments[i];
					break;
				}
			}
		}

		private void ObserveStatus(ExpStatus status, bool producedThisUpdate)
		{
			// Require a producing state before Waiting. This makes unlocking part-local:
			// a subject completed previously by another vessel doesn't unlock a fresh part.
			if (status == ExpStatus.Running || status == ExpStatus.Forced || producedThisUpdate)
				observedProducing = true;

			if (observedProducing && status == ExpStatus.Waiting)
				unlocked = true;
		}

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiName = "#KERBALISM_Module_Experiment_ViewImage", active = false, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]
		public void ViewImage()
		{
			if (!unlocked || imageLoading)
				return;

			StartCoroutine(ShowImageCoroutine());
		}

		private string NormalizedImageUrl
		{
			get
			{
				string value = image_url?.Trim();
				if (string.IsNullOrEmpty(value))
					return string.Empty;

				if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
					&& !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
					value = "https://" + value.TrimStart('/');

				return Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
					&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
					? uri.AbsoluteUri
					: string.Empty;
			}
		}

		private string ImageWindowTitle
		{
			get
			{
				// SolarScience titles are often en-us-only; Format returns the raw #tag when missing.
				if (!string.IsNullOrEmpty(image_title))
				{
					string formatted = Localizer.Format(image_title);
					if (!string.IsNullOrEmpty(formatted) && formatted != image_title)
						return formatted;
				}

				return experiment?.ExpInfo?.Title ?? Local.Module_Experiment_ViewImage;
			}
		}

		private IEnumerator ShowImageCoroutine()
		{
			imageLoading = true;

			try
			{
				DismissDialog();
				ReleaseOwnedTexture();
				imageTexture = null;

				// Instant popup like SolarScience: local backup first, web refresh in place.
				if (!TryCreateDisplayTexture())
				{
					ScreenMessages.PostScreenMessage(Local.Module_Experiment_ViewImageUnavailable, 3f, ScreenMessageStyle.UPPER_CENTER);
					yield break;
				}

				SpawnImageDialog();

				string url = NormalizedImageUrl;
				if (string.IsNullOrEmpty(url))
					yield break;

#pragma warning disable CS0618 // WWW is obsolete but is the KSP 1.12 compatible image loader.
				WWW www = null;
				try
				{
					www = new WWW(url);
				}
				catch (Exception ex)
				{
					Lib.Log($"Experiment image URL is invalid ({experiment_id}): {ex.Message}", Lib.LogLevel.Message);
				}

				if (www == null)
					yield break;

				try
				{
					yield return www;
					if (!string.IsNullOrEmpty(www.error))
					{
						Lib.Log($"Experiment image download failed ({experiment_id}): {www.error}", Lib.LogLevel.Message);
						yield break;
					}

					// DialogGUIImage keeps this texture reference; overwrite pixels in place.
					if (imageTexture != null && imageDialog != null)
						www.LoadImageIntoTexture(imageTexture);
				}
				finally
				{
					www.Dispose();
				}
#pragma warning restore CS0618
			}
			finally
			{
				// Also runs when Unity disposes the coroutine during scene/part teardown.
				imageLoading = false;
			}
		}

		/// <summary>
		/// Build an owned writable texture seeded from the GameDatabase backup when possible.
		/// Avoids mutating shared GameDatabase textures during the later web overwrite.
		/// </summary>
		private bool TryCreateDisplayTexture()
		{
			Texture2D backup = null;
			if (!string.IsNullOrEmpty(image_backup) && GameDatabase.Instance.ExistsTexture(image_backup))
				backup = GameDatabase.Instance.GetTexture(image_backup, false);

			if (backup != null)
			{
				try
				{
					imageTexture = new Texture2D(backup.width, backup.height, TextureFormat.ARGB32, false);
					imageTexture.SetPixels32(backup.GetPixels32());
					imageTexture.Apply();
					ownsImageTexture = true;
					return true;
				}
				catch
				{
					// Non-readable GameDatabase texture: fall through to a blank placeholder.
					ReleaseOwnedTexture();
					imageTexture = null;
				}

				// Fallback: show shared backup immediately (no web in-place overwrite of this instance).
				// A separate owned texture is still used for the web path below if placeholder works.
			}

			if (backup != null)
			{
				// Use a dark placeholder of the backup size; web download will replace pixels.
				imageTexture = new Texture2D(Mathf.Max(8, backup.width), Mathf.Max(8, backup.height), TextureFormat.ARGB32, false);
			}
			else if (!string.IsNullOrEmpty(NormalizedImageUrl))
			{
				imageTexture = new Texture2D(512, 512, TextureFormat.ARGB32, false);
			}
			else
			{
				return false;
			}

			Color32[] pixels = new Color32[imageTexture.width * imageTexture.height];
			Color32 fill = new Color32(0, 0, 0, 255);
			for (int i = 0; i < pixels.Length; i++)
				pixels[i] = fill;
			imageTexture.SetPixels32(pixels);
			imageTexture.Apply();
			ownsImageTexture = true;
			return true;
		}

		private void SpawnImageDialog()
		{
			if (imageTexture == null)
				return;

			float displayHeight = Mathf.Clamp(imageTexture.height * 0.5f, 256f, 640f);
			float displayWidth = displayHeight * ((float)imageTexture.width / imageTexture.height);

			imageDialog = PopupDialog.SpawnPopupDialog(
				new Vector2(0.5f, 0.5f),
				new Vector2(0.5f, 0.5f),
				new MultiOptionDialog(
					"KerbalismExperimentResultImage",
					string.Empty,
					ImageWindowTitle,
					HighLogic.UISkin,
					new Rect(0.5f, 0.5f, displayWidth, displayHeight + 40f),
					new DialogGUIVerticalLayout(
						new DialogGUIImage(new Vector2(displayWidth, displayHeight), Vector2.zero, Color.white, imageTexture),
						new DialogGUIButton(Localizer.Format("#autoLOC_149410"), DismissDialog, true)
					)
				),
				false,
				HighLogic.UISkin);

			imageDialog?.SetDraggable(true);
		}

		private void DismissDialog()
		{
			if (imageDialog != null)
				imageDialog.Dismiss();
			imageDialog = null;
		}

		private void ReleaseOwnedTexture()
		{
			if (ownsImageTexture && imageTexture != null)
				Destroy(imageTexture);
			ownsImageTexture = false;
		}

		public void OnDestroy()
		{
			StopAllCoroutines();
			DismissDialog();
			ReleaseOwnedTexture();
			imageLoading = false;
		}

		/// <summary>Kerbalism background API: track this part's experiment state while unloaded.</summary>
		public static string BackgroundUpdate(
			Vessel vessel,
			ProtoPartSnapshot protoPart,
			ProtoPartModuleSnapshot protoModule,
			PartModule modulePrefab,
			Part partPrefab,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			double elapsedSeconds)
		{
			ExperimentResultImage prefab = modulePrefab as ExperimentResultImage;
			if (prefab == null)
				return "Experiment Result Image";

			bool isUnlocked = Lib.Proto.GetBool(protoModule, "unlocked");
			bool wasProducing = Lib.Proto.GetBool(protoModule, "observedProducing");

			for (int i = 0; i < protoPart.modules.Count; i++)
			{
				ProtoPartModuleSnapshot candidate = protoPart.modules[i];
				if (candidate.moduleName != "Experiment"
					|| Lib.Proto.GetString(candidate, "experiment_id") != prefab.experiment_id)
					continue;

				ExpStatus status = Lib.Proto.GetEnum(candidate, "status", ExpStatus.Stopped);
				bool producedThisUpdate = Lib.Proto.GetDouble(candidate, "prodFactor") > 0.0;
				if (status == ExpStatus.Running || status == ExpStatus.Forced || producedThisUpdate)
					wasProducing = true;

				if (wasProducing && status == ExpStatus.Waiting)
					isUnlocked = true;
				break;
			}

			Lib.Proto.Set(protoModule, "observedProducing", wasProducing);
			Lib.Proto.Set(protoModule, "unlocked", isUnlocked);
			return "Experiment Result Image";
		}
	}
}
