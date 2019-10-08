using KERBALISM.KsmGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static KERBALISM.ScienceDB;

namespace KERBALISM
{
	public class ExperimentArchive : KsmGuiVerticalLayout
	{
		public KsmGuiToggle KnownSubjectsToggle {get; private set;}
		public List<BodyContainer> BodyContainers = new List<BodyContainer>();


		public ExperimentArchive(KsmGuiBase parent, ExperimentInfo expInfo) : base(parent)
		{
			KnownSubjectsToggle = new KsmGuiToggle(this, "Show known subjects only", true, ToggleKnownSubjects);

			KsmGuiBase listHeader = new KsmGuiBase(this);
			listHeader.SetLayoutElement(true, false, -1, 16);
			listHeader.AddImageComponentWithColor(KsmGuiStyle.boxColor);

			KsmGuiText rndHeaderText = new KsmGuiText(listHeader, "RnD", "Science points\nretrieved in RnD", TextAlignmentOptions.Left);
			rndHeaderText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Science);
			rndHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			rndHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 10, 0);
			rndHeaderText.TopTransform.SetSizeDelta(50, 16);

			KsmGuiText flightHeaderText = new KsmGuiText(listHeader, "Flight", "Science points\ncollected in all vessels", TextAlignmentOptions.Left);
			flightHeaderText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Science);
			flightHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			flightHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 60, 0);
			flightHeaderText.TopTransform.SetSizeDelta(50, 16);

			KsmGuiText valueHeaderText = new KsmGuiText(listHeader, "Value", "Remaining value of the subject", TextAlignmentOptions.Left);
			valueHeaderText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Science);
			valueHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			valueHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 110, 0);
			valueHeaderText.TopTransform.SetSizeDelta(50, 16);

			KsmGuiText completedHeaderText = new KsmGuiText(listHeader, "Completed", "How many times the subject\nhas been retrieved in RnD", TextAlignmentOptions.Left);
			completedHeaderText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Yellow);
			completedHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			completedHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 160, 0);
			completedHeaderText.TopTransform.SetSizeDelta(100, 16);

			KsmGuiVerticalScrollView scrollView = new KsmGuiVerticalScrollView(this);
			scrollView.SetLayoutElement(true, false, 320, -1, -1, 250);
			scrollView.ContentGroup.padding = new RectOffset(0, 5, 5, 5);

			foreach (ObjectPair<int, SituationsBiomesSubject> bodySubjects in GetSubjectsForExperiment(expInfo))
			{
				CelestialBody body = FlightGlobals.Bodies[bodySubjects.Key];
				BodyContainer bodyEntry = new BodyContainer(scrollView, body, bodySubjects.Value);
				BodyContainers.Add(bodyEntry);
			}

			SetUpdateAction(CheckKnownSubjects, 50);
			CheckKnownSubjects();
			ToggleKnownSubjects(true);
		}

		public void ToggleKnownSubjects(bool onlyKnown)
		{
			foreach (BodyContainer body in BodyContainers)
			{
				if (onlyKnown && !body.isKnown)
				{
					body.Enabled = false;
					continue;
				}
				body.Enabled = true;
				body.ToggleBody(body.isKnown);

				for (int i = 0; i < body.SubjectsContainer.SubjectLines.Count; i++)
				{
					SubjectLine subjectLine = body.SubjectsContainer.SubjectLines[i];
					subjectLine.Enabled = (onlyKnown && subjectLine.isKnown) || !onlyKnown;
					subjectLine.SituationLine.Enabled = (onlyKnown && subjectLine.SituationLine.IsKnown) || !onlyKnown;
				}
			}
			RebuildLayout();
		}

		private void CheckKnownSubjects()
		{
			foreach (BodyContainer body in BodyContainers)
			{
				body.isKnown = false;
				SituationLine currentSituation = null;
				foreach (SubjectLine subjectLine in body.SubjectsContainer.SubjectLines)
				{
					if (currentSituation != subjectLine.SituationLine)
					{
						currentSituation = subjectLine.SituationLine;
						currentSituation.IsKnown = false;
					}

					if (subjectLine.SubjectData.ScienceCollectedTotal > 0.0)
					{
						subjectLine.isKnown = true;
						currentSituation.IsKnown |= true;
						body.isKnown |= true;
					}
				}
			}
		}

		public class BodyContainer: KsmGuiVerticalLayout
		{
			public bool isKnown;
			public SubjectsContainer SubjectsContainer { get; private set; }
			KsmGuiIconButton bodyToggle;

			public BodyContainer(KsmGuiBase parent, CelestialBody body, SituationsBiomesSubject situationsAndSubjects) : base(parent)
			{
				KsmGuiHeader header = new KsmGuiHeader(this, body.name, KsmGuiStyle.boxColor);
				header.TextObject.TextComponent.fontStyle = FontStyles.Bold;
				header.TextObject.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Orange);
				header.TextObject.TextComponent.alignment = TextAlignmentOptions.Left;
				bodyToggle = new KsmGuiIconButton(header, Textures.KsmGuiTexHeaderArrowsUp, ToggleBody);
				bodyToggle.SetIconColor(Lib.Kolor.Orange);
				bodyToggle.MoveAsFirstChild();

				SubjectsContainer = new SubjectsContainer(this, body, situationsAndSubjects);
			}

			public void ToggleBody()
			{
				ToggleBody(!SubjectsContainer.Enabled);
			}

			public void ToggleBody(bool enable)
			{
				SubjectsContainer.Enabled = enable;
				bodyToggle.SetIconTexture(enable ? Textures.KsmGuiTexHeaderArrowsUp : Textures.KsmGuiTexHeaderArrowsDown);
				RebuildLayout();
			}
		}

		public class SubjectsContainer : KsmGuiVerticalLayout
		{
			public List<SubjectLine> SubjectLines { get; private set; } = new List<SubjectLine>();

			public SubjectsContainer(KsmGuiBase parent, CelestialBody body, SituationsBiomesSubject situationsAndSubjects) : base(parent)
			{
				foreach (ObjectPair<ScienceSituation, BiomesSubject> situation in situationsAndSubjects)
				{

					SituationLine situationLine = new SituationLine(this, situation.Key.Title());
					//situationLine.SetLayoutElement(true, false, -1, 14);
					situationLine.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 5);
					situationLine.TopTransform.SetSizeDelta(150, 14);
					situationLine.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Yellow);
					situationLine.TextComponent.fontStyle = FontStyles.Bold;

					foreach (ObjectPair<int, List<SubjectData>> subjects in situation.Value)
					{
						foreach (SubjectData subjectData in subjects.Value)
						{
							SubjectLine subject;
							
							if (subjects.Key >= 0)
								subject = new SubjectLine(this, body.BiomeMap.Attributes[subjects.Key].displayname, subjectData, situationLine);
							else
								subject = new SubjectLine(this, string.Empty, subjectData, situationLine);

							SubjectLines.Add(subject);
						}
						
					}
				}
			}
		}

		public class SituationLine : KsmGuiText
		{
			public bool IsKnown;
			public SituationLine(KsmGuiBase parent, string situation) : base(parent, situation) { }
		}

		public class SubjectLine : KsmGuiBase
		{
			public bool isKnown;
			public SituationLine SituationLine { get; private set; }
			public SubjectData SubjectData { get; private set; }
			private KsmGuiText rndText;
			private KsmGuiText flightText;
			private KsmGuiText valueText;
			private KsmGuiText completedText;

			

			public SubjectLine(KsmGuiBase parent, string biomeName, SubjectData subject, SituationLine situationLine) : base(parent)
			{
				SituationLine = situationLine;
				SubjectData = subject;
				SetLayoutElement(true, false, -1, 14);

				rndText = new KsmGuiText(this, "RnD");
				rndText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Science);
				rndText.TextComponent.fontStyle = FontStyles.Bold;
				rndText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 10);
				rndText.TopTransform.SetSizeDelta(50, 14);

				flightText = new KsmGuiText(this, "flight", null, TextAlignmentOptions.Left);
				flightText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Science);
				flightText.TextComponent.fontStyle = FontStyles.Bold;
				flightText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 60);
				flightText.TopTransform.SetSizeDelta(50, 14);

				valueText = new KsmGuiText(this, "value", null, TextAlignmentOptions.Left);
				valueText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Science);
				valueText.TextComponent.fontStyle = FontStyles.Bold;
				valueText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 110);
				valueText.TopTransform.SetSizeDelta(50, 14);

				completedText = new KsmGuiText(this, "completed", null, TextAlignmentOptions.Left);
				completedText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Yellow);
				completedText.TextComponent.fontStyle = FontStyles.Bold;
				completedText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 160);
				completedText.TopTransform.SetSizeDelta(50, 14);

				if (!string.IsNullOrEmpty(biomeName))
				{
					KsmGuiText biomeText = new KsmGuiText(this, biomeName, null, TextAlignmentOptions.Left);
					biomeText.TopTransform.SetAnchorsAndPosition(TextAnchor.UpperLeft, TextAnchor.UpperLeft, 215);
					biomeText.TopTransform.SetSizeDelta(200, 14);
				}

				SetUpdateAction(Update, 50);
				Update();
				

				//IsKnown = SubjectData.ScienceCollectedTotal > 0.0;
				//Enabled = IsKnown;
			}

			private void Update()
			{
				rndText.SetText(SubjectData.ScienceRetrievedInKSC.ToString("0.0;--;--"));
				flightText.SetText(SubjectData.ScienceCollectedInFlight.ToString("+0.0;--;--"));
				valueText.SetText(SubjectData.ScienceRemainingTotal.ToString("0.0;--;--"));
				completedText.SetText(SubjectData.PercentRetrieved.ToString("0.0x;--;--"));
			}

		}
	}




}
