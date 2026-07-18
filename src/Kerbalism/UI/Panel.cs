using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	// store and render a simple structured ui
	public sealed class Panel
	{
		public enum PanelType
		{
			unknown,
			telemetry,
			data,
			scripts,
			failures,
			config,
			log,
			connection
		}

		public Panel()
		{
			headers = new List<Header>();
			sections = new List<Section>();
			callbacks = new List<Action>();
			win_title = string.Empty;
			min_width = Styles.ScaleWidthFloat(280.0f);
			compact_scrollbar = false;
			paneltype = PanelType.unknown;
		}

		public void Clear()
		{
			headers.Clear();
			sections.Clear();
			win_title = string.Empty;
			min_width = Styles.ScaleWidthFloat(280.0f);
			compact_scrollbar = false;
			paneltype = PanelType.unknown;
		}

		public void AddHeader(string label, string tooltip = "", Action click = null)
		{
			Header h = new Header
			{
				label = label,
				tooltip = tooltip,
				click = click,
				icons = new List<Icon>(),
				leftIcon = null
			};
			headers.Add(h);
		}

		///<summary> Sets the last added header or content leading icon (doesn't support sections)</summary>
		public void SetLeftIcon(Texture2D texture, string tooltip = "", Action click = null)
		{
			Icon i = new Icon
			{
				texture = texture,
				tooltip = tooltip,
				click = click
			};

			if (sections.Count > 0)
			{
				Section p = sections[sections.Count - 1];
				p.entries[p.entries.Count - 1].leftIcon = i;
			}
			else if (headers.Count > 0)
			{
				Header h = headers[headers.Count - 1];
				h.leftIcon = i;
			}
		}

		public void AddSection(string title, string desc = "", Action left = null, Action right = null, Boolean sort = false, Action click = null, Boolean pin = false)
		{
			Section p = new Section
			{
				title = title,
				desc = desc,
				left = left,
				right = right,
				click = click,
				pin = pin,
				sort = sort,
				needsSort = false,
				entries = new List<Entry>()
			};
			sections.Add(p);
		}

		public void AddContent(string label, string value = "", string tooltip = "", Action click = null, Action hover = null)
		{
			Entry e = new Entry
			{
				label = label,
				value = value,
				tooltip = tooltip,
				click = click,
				hover = hover,
				selectable = false,
				icons = new List<Icon>()
			};
			if (sections.Count > 0) {
				Section section = sections[sections.Count - 1];
				section.entries.Add(e);
				section.needsSort = section.sort;
			}
		}

		/// <summary>Adds an opt-in, whole-row selectable entry that can grow when its label wraps.</summary>
		public void AddSelectableContent(string label, string value, string tooltip, Action click)
		{
			Entry e = new Entry
			{
				label = label,
				value = value,
				tooltip = tooltip,
				click = click,
				hover = null,
				selectable = true,
				icons = new List<Icon>()
			};
			if (sections.Count > 0)
			{
				Section section = sections[sections.Count - 1];
				section.entries.Add(e);
				section.needsSort = section.sort;
			}
		}

		///<summary> Adds an icon to the last added header or content (doesn't support sections) </summary>
		public void AddRightIcon(Texture2D texture, string tooltip = "", Action click = null)
		{
			Icon i = new Icon
			{
				texture = texture,
				tooltip = tooltip,
				click = click
			};
			if (sections.Count > 0)
			{
				Section p = sections[sections.Count - 1];
				p.entries[p.entries.Count - 1].icons.Add(i);
			}
			else if (headers.Count > 0)
			{
				Header h = headers[headers.Count - 1];
				h.icons.Add(i);
			}
		}

		/// <summary>Render section titles explicitly pinned above the panel scroll view.</summary>
		public void RenderPinned()
		{
			foreach (Section section in sections)
				if (section.pin) RenderSectionTitle(section);
		}

		public void Render()
		{
			// headers
			foreach (Header h in headers)
			{
				GUILayout.BeginHorizontal(Styles.entry_container);
				if (h.leftIcon != null)
				{
					GUILayout.Label(new GUIContent(h.leftIcon.texture, h.leftIcon.tooltip), Styles.left_icon);
					if (h.leftIcon.click != null && Lib.IsClicked())
						callbacks.Add(h.leftIcon.click);
				}
				GUILayout.Label(new GUIContent(h.label, h.tooltip), Styles.entry_label_nowrap);
				if (h.click != null && Lib.IsClicked()) callbacks.Add(h.click);
				foreach (Icon i in h.icons)
				{
					GUILayout.Label(new GUIContent(i.texture, i.tooltip), Styles.right_icon);
					if (i.click != null && Lib.IsClicked()) callbacks.Add(i.click);
				}
				GUILayout.EndHorizontal();
				GUILayout.Space(Styles.ScaleFloat(10.0f));
			}

			// sections
			foreach (Section p in sections)
			{
				if (!p.pin) RenderSectionTitle(p);

				// description
				if (p.desc.Length > 0)
				{
					GUILayout.BeginHorizontal(Styles.desc_container);
					GUILayout.Label(p.desc, Styles.desc);
					GUILayout.EndHorizontal();
				}

				// entries
				if(p.needsSort) {
					p.needsSort = false;
					p.entries.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.Ordinal));
				}
				foreach (Entry e in p.entries)
				{
					if (e.selectable)
						GUILayout.BeginHorizontal(Styles.entry_container_wrap, GUILayout.MinHeight(Styles.entry_container.fixedHeight));
					else
						GUILayout.BeginHorizontal(Styles.entry_container);
					if (e.leftIcon != null)
					{
						GUILayout.Label(new GUIContent(e.leftIcon.texture, e.leftIcon.tooltip), Styles.left_icon);
						if (e.leftIcon.click != null && Lib.IsClicked())
							callbacks.Add(e.leftIcon.click);
					}
					if (e.selectable)
						GUILayout.Label(new GUIContent(e.label, e.tooltip), Styles.entry_label);
					else
						GUILayout.Label(new GUIContent(e.label, e.tooltip), Styles.entry_label, GUILayout.Height(Styles.entry_label.fontSize));
					if (e.hover != null && Lib.IsHover()) callbacks.Add(e.hover);
					if (e.selectable)
						GUILayout.Label(new GUIContent(e.value, e.tooltip), Styles.entry_value, GUILayout.Width(Styles.ScaleWidthFloat(20.0f)));
					else
						GUILayout.Label(new GUIContent(e.value, e.tooltip), Styles.entry_value, GUILayout.Height(Styles.entry_value.fontSize));
					if (!e.selectable && e.click != null && Lib.IsClicked()) callbacks.Add(e.click);
					if (e.hover != null && Lib.IsHover()) callbacks.Add(e.hover);
					foreach (Icon i in e.icons)
					{
						GUILayout.Label(new GUIContent(i.texture, i.tooltip), Styles.right_icon);
						if (i.click != null && Lib.IsClicked()) callbacks.Add(i.click);
					}
					GUILayout.EndHorizontal();

					if (e.selectable)
					{
						Rect rowRect = GUILayoutUtility.GetLastRect();
						if (Event.current.type == EventType.MouseDown
							&& Event.current.button == 0
							&& rowRect.Contains(Event.current.mousePosition))
						{
							callbacks.Add(e.click);
							Event.current.Use();
						}
						else if (Event.current.type == EventType.Repaint
							&& rowRect.Contains(Event.current.mousePosition))
						{
							Color previousColor = GUI.color;
							GUI.color = new Color(1.0f, 1.0f, 1.0f, 0.12f);
							GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
							GUI.color = previousColor;
						}
					}
				}

				// spacing
				GUILayout.Space(Styles.ScaleFloat(10.0f));
			}

			// call callbacks
			if (Event.current.type == EventType.Repaint)
			{
				foreach (Action func in callbacks) func();
				callbacks.Clear();
			}
		}

		void RenderSectionTitle(Section section)
		{
			GUILayout.BeginHorizontal(Styles.section_container);
			if (section.left != null)
			{
				GUILayout.Label(Textures.left_arrow, Styles.left_icon);
				if (Lib.IsClicked()) callbacks.Add(section.left);
			}
			GUILayout.Label(section.title, Styles.section_text);
			if (section.right != null)
			{
				GUILayout.Label(Textures.right_arrow, Styles.right_icon);
				if (Lib.IsClicked()) callbacks.Add(section.right);
			}
			GUILayout.EndHorizontal();
			if (section.click != null && Event.current.type == EventType.MouseDown
				&& Event.current.button == 0
				&& GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
			{
				callbacks.Add(section.click);
				Event.current.Use();
			}
		}

		public float Height()
		{
			float h = 0.0f;

			h += Styles.ScaleFloat((float)headers.Count * 27.0f);

			foreach (Section p in sections)
			{
				h += Styles.ScaleFloat(34.0f);
				foreach (Entry e in p.entries)
				{
					if (e.selectable)
					{
						float labelWidth = Math.Max(Styles.ScaleWidthFloat(40.0f),
							min_width - Styles.ScaleWidthFloat(50.0f));
						h += Math.Max(Styles.entry_container.fixedHeight,
							Styles.entry_label.CalcHeight(new GUIContent(e.label), labelWidth));
					}
					else
					{
						h += Styles.entry_container.fixedHeight;
					}
				}
				if (p.desc.Length > 0)
				{
					h += Styles.desc.CalcHeight(new GUIContent(p.desc), min_width - Styles.ScaleWidthFloat(20.0f));
				}
			}

			return h;
		}

		// utility: decrement an index, warping around 0
		public void Prev(ref int index, int count)
		{
			index = (index == 0 ? count : index) - 1;
		}

		// utility: increment an index, warping around a max
		public void Next(ref int index, int count)
		{
			index = (index + 1) % count;
		}

		// utility: toggle a flag
		public void Toggle(ref bool b)
		{
			b = !b;
		}

		// merge another panel with this one
		public void Add(Panel p)
		{
			headers.AddRange(p.headers);
			sections.AddRange(p.sections);
		}

		// collapse all sections into one
		public void Collapse(string title)
		{
			if (sections.Count > 0)
			{
				sections[0].title = title;
				for (int i = 1; i < sections.Count; ++i) sections[0].entries.AddRange(sections[i].entries);
			}
			while (sections.Count > 1) sections.RemoveAt(sections.Count - 1);
		}

		// return true if panel has no sections or titles
		public bool Empty()
		{
			return sections.Count == 0 && headers.Count == 0;
		}

		// set title metadata
		public void Title(string s)
		{
			win_title = s;
		}

		// set width metadata
		// - width never shrink
		public void Width(float w)
		{
			min_width = Math.Max(w, min_width);
		}

		public void UseCompactScrollbar()
		{
			compact_scrollbar = true;
		}

		// get medata
		public string Title() { return win_title; }
		public float Width() { return min_width; }
		public bool UsesCompactScrollbar() { return compact_scrollbar; }

		sealed class Header
		{
			public string label;
			public string tooltip;
			public Action click;
			public List<Icon> icons;
			public Icon leftIcon;
		}

		sealed class Section
		{
			public string title;
			public string desc;
			public Action left;
			public Action right;
			public Action click;
			public Boolean pin;
			public Boolean sort;
			public Boolean needsSort;
			public List<Entry> entries;
		}

		sealed class Entry
		{
			public string label;
			public string value;
			public string tooltip;
			public Action click;
			public Action hover;
			public Boolean selectable;
			public List<Icon> icons;
			public Icon leftIcon;
		}

		sealed class Icon
		{
			public Texture2D texture;
			public string tooltip;
			public Action click;
		}

		List<Header> headers;    // fat entries to show before the first section
		List<Section> sections;  // set of sections
		List<Action> callbacks;  // functions to call on input events
		string win_title;        // metadata stored in panel
		float min_width;         // metadata stored in panel
		bool compact_scrollbar;  // opt-in compact vertical scrollbar
		public PanelType paneltype;
	}
} // KERBALISM
