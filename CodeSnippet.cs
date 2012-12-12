/*
 * CodeSnippet.cs:  A simple code snippet addin for tomboy
 *
 * Copyright 2012,  Chaos Zhuang <frzhuang@gmail.com>
 *
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using System.Collections;
using System.Collections.Generic;
using Pango;

using Mono.Unix;
using Tomboy;
using Gtk;
using GtkSourceView;
using GLib;

namespace Tomboy.CodeSnippet
{
	public class CodeSnippetWindow : Gtk.VBox
	{
		static GtkSourceView.SourceLanguageManager manager; // could be global
		GtkSourceView.SourceBuffer sourceBuf;
		GtkSourceView.SourceView source;
		Gtk.ScrolledWindow scroll;
		int Width, Height; // the desired size of the widget
		bool changed;
		public Gtk.TextMark cs_pos; // code is inserted between before the mark
		public Gtk.TextMark cs_pos_; // code is inserted between after the mark
		
		public string Language {
			get {
				return sourceBuf.Language.Name.ToLower ();
			}
		}

		public string rawCode {
			get {
				return sourceBuf.Text;
			}
		}

		public void writeCode (string rawCode, string lang)
		{
			sourceBuf.Text = rawCode;
			
			GtkSourceView.SourceLanguage language = manager.GetLanguage (lang);
			if (language != null) 
				sourceBuf.Language = language;
			int tmp; 
			// maximum displayed line number is 15 ?
			source.GetLineYrange (sourceBuf.StartIter, out tmp, out Height);
			Height *= (sourceBuf.LineCount > 15 ? 15 : sourceBuf.LineCount);
			Height += (2 * (int)source.BorderWidth) + 10;
			SetSizeRequest (Width, Height);
		}

		public void update ()
		{
			changed = false;
		}

		public bool needUpdate ()
		{
			return changed ;
		}

		public CodeSnippetWindow () : base(false, 4)
		{	
			manager = new GtkSourceView.SourceLanguageManager ();
			GtkSourceView.SourceLanguage language = manager.GetLanguage ("c");
			
			GtkSourceView.SourceStyleSchemeManager schemeManager = 
							new GtkSourceView.SourceStyleSchemeManager ();
			GtkSourceView.SourceStyleScheme styleScheme = schemeManager.GetScheme ("oblivion");
			
			sourceBuf = new GtkSourceView.SourceBuffer (language);
			sourceBuf.HighlightMatchingBrackets = true;
			sourceBuf.HighlightSyntax = true;
			sourceBuf.StyleScheme = styleScheme;
			
			source = new GtkSourceView.SourceView (sourceBuf);		
			source.BorderWidth = 3;
			source.AutoIndent = true;
			source.IndentOnTab = true;
			source.IndentWidth = 4;
			source.TabWidth = 4;
			source.ShowLineNumbers = true;
			FontDescription font_desc = FontDescription.FromString ("monospace size:small");
			source.ModifyFont(font_desc);
			
			scroll = new Gtk.ScrolledWindow ();
			scroll.Add (source);
			PackStart (scroll, true, true, 2);

			changed = false;
			Width = 300;
			Height = 200;
			SetSizeRequest (Width, Height);
			
			source.FocusInEvent += OnFocusIn;
			source.FocusOutEvent += OnFocusOut;
			source.PopulatePopup += OnPopulatePopup;
		}

		static void MarkupLabel (Gtk.MenuItem item)
		{
			Gtk.Label label = (Gtk.Label)item.Child;
			label.UseMarkup = true;
			label.UseUnderline = true;
		}

		void  OnPopulatePopup (object o, PopulatePopupArgs args)
		{
			Gtk.MenuItem spacer = new Gtk.SeparatorMenuItem ();
			spacer.Show ();
			
			Gtk.CheckMenuItem edit = new Gtk.CheckMenuItem (
				Catalog.GetString ("Edit Code Snippet"));
			MarkupLabel (edit);
			edit.Activated += OnEditActivate;
			edit.Show ();
			
			Gtk.ImageMenuItem lang_select = new Gtk.ImageMenuItem (
						Catalog.GetString ("Select Language"));
			lang_select.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			lang_select .Show ();
			Gtk.Menu langs_menu = new Gtk.Menu ();
						
			//default language should be setted
			Gtk.RadioMenuItem pre_item = new Gtk.RadioMenuItem (Language);

			MarkupLabel (pre_item);
			pre_item.Active = true;
			langs_menu.Append (pre_item);
			pre_item.Activated += OnLanguageSelected;
			pre_item.Show ();

			string [] langs = manager.LanguageIds;
			ArrayList lang_array = new ArrayList (langs);
			lang_array.Sort ();
			foreach (String lang in lang_array) {
				if (lang.Equals (Language))
					continue;
				Gtk.RadioMenuItem tmp = new Gtk.RadioMenuItem (
						pre_item.Group, Catalog.GetString (lang));
				MarkupLabel (tmp);
				langs_menu.Append (tmp);
				tmp.Activated += OnLanguageSelected;
				tmp.Show ();
				pre_item = tmp;
			}
			lang_select.Submenu = langs_menu; 
			args.Menu.Prepend (spacer);
			args.Menu.Prepend (edit);
			args.Menu.Prepend (lang_select);		
		}

		void OnLanguageSelected (object sender, EventArgs e)
		{			
			Gtk.RadioMenuItem item = (Gtk.RadioMenuItem)sender;	
			if (item.Active != true)
				return;
			Gtk.Label label = (Gtk.Label)item.Child;
			sourceBuf.Language = manager.GetLanguage (label.Text);
			item.Active = true;
			changed = true;
		}

		void  OnEditActivate (object sender, EventArgs e)
		{
			source.Editable = true;
			source.HighlightCurrentLine = true;
			
			int tmp;
			source.GetLineYrange (sourceBuf.StartIter, out tmp, out Height);
			Height *= (sourceBuf.LineCount < 15 ? 15 : sourceBuf.LineCount);
			//I still don't figure out why tomboy would crash if a smaller height was setted here
			Height = Height + 1; 
			Height += (2 * (int)source.BorderWidth) + 10;
			SetSizeRequest (Width, Height);  //Sets the minimum size of a widget
						
			changed = true;

		}
		
		void OnFocusIn (object sender, FocusInEventArgs  args)
		{				
			//get the actual size of the top level window of current widget
			if (Toplevel.Allocation.Width - 20 > 20)
				Width = Toplevel.Allocation.Width - 20;
			SetSizeRequest (Width, Height);  //Sets the minimum size of a widget
		}

		void OnFocusOut (object sender, FocusOutEventArgs  args)
		{
			int tmp;
			// maximum displayed line number
			source.GetLineYrange (sourceBuf.StartIter, out tmp, out Height);
			Height *= (sourceBuf.LineCount < 15 ? sourceBuf.LineCount : 15);
			Height += (2 * (int)source.BorderWidth) + 10;
					
			SetSizeRequest (Width, Height);  //Sets the minimum size of a widget
			
			//changed = true;
			source.Editable = false;
			source.HighlightCurrentLine = false;
		}
	}

	public class CodeSnippetNoteAddin : NoteAddin
	{
		Gtk.MenuItem item;
		List<CodeSnippetWindow> csList;
		static bool debug;
		bool cs_changed;
		
		static CodeSnippetNoteAddin ()
		{
			debug = true;
		}
		// Called when the NoteAddin is attached to a Note
		public override void Initialize ()
		{
			csList = new List<CodeSnippetWindow> (); 
			
			if (Note.TagTable.Lookup ("codesnippet_code") == null) {
				NoteTag codesnippet_code_tag = new NoteTag ("codesnippet_code");
				/* Invisible causes instability, replaced by Size = 1
				 * But it seems not to work in Windows. */
				codesnippet_code_tag.Invisible = true;
				codesnippet_code_tag.Size = 1;
				codesnippet_code_tag.Editable = false;
				codesnippet_code_tag.CanSerialize = false;
				Note.TagTable.Add (codesnippet_code_tag);
			}
		}

		// Called when a note is deleted and also when
		// the addin is disabled.
		public override void Shutdown ()
		{ 
			foreach (CodeSnippetWindow csTmp in csList) {
				
				Gtk.TextIter cs_start = Buffer.GetIterAtMark (csTmp.cs_pos_); 
				Gtk.TextIter cs_end = Buffer.GetIterAtMark (csTmp.cs_pos);
				if (cs_start.Offset != cs_end.Offset)
					Buffer.RemoveTag ("codesnippet_code", cs_start, cs_end);
				cs_start = Buffer.GetIterAtMark (csTmp.cs_pos); 
				cs_end = cs_start; 
				cs_end.ForwardChar ();
				
				Buffer.Delete (ref cs_start, ref cs_end);
				Buffer.DeleteMark (csTmp.cs_pos);
				Buffer.DeleteMark (csTmp.cs_pos_);
			} 
			csList.Clear ();
			if (item != null) 
				item.Activated -= OnMenuItemActivated;
			
			if (HasWindow) {
				Window.Editor.MoveCursor -= OnMoveCursor;
				Window.Editor.FocusInEvent -= OnFocusIn;
				Window.Editor.FocusOutEvent -= OnFocusOut;	
				Window.Editor.DeleteFromCursor -= OnDeleteRange;
				Window.Editor.Backspace -= OnBackspace;
			}

		}

		public override void OnNoteOpened ()
		{
			item = new Gtk.MenuItem (Catalog.GetString ("Insert Code Snippet"));
			item.Activated += OnMenuItemActivated;
			item.AddAccelerator ("activate", Window.AccelGroup, 
					     (uint)Gdk.Key.d, Gdk.ModifierType.ControlMask,
					     Gtk.AccelFlags.Visible);
			item.Show ();
			AddPluginMenuItem (item);
			
			Window.Editor.DeleteFromCursor += OnDeleteRange;
			Window.Editor.Backspace += OnBackspace;
			Window.Editor.MoveCursor += OnMoveCursor;
			Window.Editor.FocusInEvent += OnFocusIn;
			Window.Editor.FocusOutEvent += OnFocusOut;
			
			checkCodeSnippet ();
			cs_changed = false;
		}

		bool findCodeSnippet (Gtk.TextIter pos, out Gtk.TextIter match_start,
		                      out Gtk.TextIter match_end, out string lang, out string code)
		{
			Gtk.TextIter match_start_tmp, match_end_tmp, lang_start, lang_end;
			lang = "c";
			match_start = pos;
			match_end = pos;
			code = "";
			if (pos.ForwardSearch ("\\<chaosCS:", 0, out match_start_tmp, out lang_start, Buffer.EndIter) &&
                        lang_start.ForwardSearch ("\\>", 0, out lang_end, out match_end_tmp, Buffer.EndIter)) {
				match_start = match_start_tmp;
				lang = lang_start.GetSlice (lang_end);
				if (!match_end_tmp.ForwardLines (2))
					return false;//skip [[chaos
				match_start_tmp = match_end_tmp;
				if (match_end_tmp.ForwardSearch ("\nchaos]]\n", 0, out match_end_tmp, out lang_end, Buffer.EndIter)) {
					code = match_start_tmp.GetSlice (match_end_tmp);
					match_end = lang_end;
					return true;
				}
			}
			return false;
		}

		void checkCodeSnippet ()
		{
			string lang;
			try {
				Gtk.TextIter pos = Buffer.StartIter;
				pos.ForwardLine (); // skip the title
				
				while (true) {
					Gtk.TextIter match_start = pos;
					Gtk.TextIter match_end = pos;
					string code;
					if (!findCodeSnippet (pos, out match_start, out match_end, out lang, out code))
						break;
					Logger.Info ("Code Snippet: Creating snippet ...");

					CodeSnippetWindow newcs = new CodeSnippetWindow ();
					newcs.writeCode (code, lang);
					
					newcs.cs_pos_ = Buffer.CreateMark (null, match_start, true);
					Buffer.ApplyTag ("codesnippet_code", match_start, match_end);
					 
					Gtk.TextChildAnchor child = Buffer.CreateChildAnchor (ref match_end);				
					newcs.cs_pos = Buffer.CreateMark (null, Buffer.GetIterAtChildAnchor (child), false);
					Window.Editor.AddChildAtAnchor (newcs, child);
					
					newcs.ShowAll ();
					
					csList.Add (newcs);
					pos = Buffer.GetIterAtMark (newcs.cs_pos);
					
				}
			} finally {
			}

		}
	 
		void OnMenuItemActivated (object sender, EventArgs args)
		{
			Gtk.TextIter cursor = Buffer.GetIterAtMark (Buffer.InsertMark);
			
			CodeSnippetWindow newcs = new CodeSnippetWindow ();
			
			Gtk.TextChildAnchor child = Buffer.CreateChildAnchor (ref cursor);
			Window.Editor.AddChildAtAnchor (newcs, child);
			
			newcs.cs_pos = Buffer.CreateMark (null, Buffer.GetIterAtChildAnchor (child), false);
			newcs.cs_pos_ = Buffer.CreateMark (null, Buffer.GetIterAtChildAnchor (child), true);

			newcs.ShowAll ();
			csList.Add (newcs);

		}

		void  OnBackspace (object sender, EventArgs e)
		{
			//anchor.Deleted
			Gtk.TextIter pos = Buffer.GetIterAtMark (Buffer.InsertMark);
			Gtk.TextTag cstag = Buffer.TagTable.Lookup ("codesnippet_code");

			Gtk.TextIter tmp = pos;
			tmp.BackwardChar ();
			if (tmp.HasTag (cstag)) {
				Logger.Info ("Removing Code Snippet at " + pos.Offset);
				csList.RemoveAll (cs_tmp => pos.Equal (Buffer.GetIterAtMark (cs_tmp.cs_pos)));
				tmp.BackwardToTagToggle (cstag);
				Buffer.Delete (ref tmp, ref pos);
			}

		}
		
		private int countMark ()
		{
			int markCounter = 0;
			Gtk.TextIter tmpIter = Buffer.StartIter;
			while (tmpIter.IsEnd == false) {
				if (tmpIter.ChildAnchor != null) {
					markCounter++;
				}
				tmpIter.BackwardChar ();
			}

			return markCounter;
		}

		private void showCSInfo ()
		{
			foreach (CodeSnippetWindow csTmp in csList) {
				System.Console.WriteLine ("--------" + 
				                          Buffer.GetIterAtMark (csTmp.cs_pos).Offset + 
				                          "--------");
				System.Console.WriteLine (csTmp.rawCode);
			}
		}

		void OnDeleteRange (object o, DeleteFromCursorArgs args)
		{
			//TODO:
			//Delete the according element in cslist when deleting anchors
			//anchor.Deleted
			DeleteCodeSnippet ();
		}

		private void DeleteCodeSnippet ()
		{
			Gtk.TextIter pos = Buffer.GetIterAtMark (Buffer.InsertMark);
			Gtk.TextTag cstag = Buffer.TagTable.Lookup ("codesnippet_code");
			Gtk.TextIter tmp = pos;
			tmp.BackwardChar ();
			if (tmp.HasTag (cstag)) {
				csList.RemoveAll (cs_tmp => pos.Equal (Buffer.GetIterAtMark (cs_tmp.cs_pos)));
				tmp.BackwardToTagToggle (cstag);
				Buffer.Delete (ref tmp, ref pos);
			}
		}
		
		
		//skip the hidden texts of a code snippet when move the cursor
		void OnMoveCursor (object sender, Gtk.MoveCursorArgs args)
		{
			Gtk.TextTag cstag = Buffer.TagTable.Lookup ("codesnippet_code");
			Gtk.TextIter cursor = Buffer.GetIterAtMark (Buffer.InsertMark);
			if (cursor.HasTag (cstag)) {
				if (args.Count >= 0)
					cursor.ForwardToTagToggle (cstag);
				else {
					cursor.BackwardToTagToggle (cstag);
					cursor.BackwardChar ();
				}
			}
			Buffer.PlaceCursor (cursor);
		}

		void OnFocusIn (object sender, Gtk.FocusInEventArgs args)
		{
			if (!cs_changed)
				return;
			
			foreach (CodeSnippetWindow csTmp in csList) {
				if (csTmp.needUpdate ()) {
					Gtk.TextIter startIter = Buffer.GetIterAtMark (csTmp.cs_pos_); 
					Gtk.TextIter EndIter = Buffer.GetIterAtMark (csTmp.cs_pos); 
					
					if (startIter.Offset != EndIter.Offset)
						Buffer.Delete (ref startIter, ref EndIter);
										
					EndIter = Buffer.GetIterAtMark (csTmp.cs_pos); 

					Buffer.InsertWithTagsByName (ref EndIter, 
					                             "\\<chaosCS:" + csTmp.Language.ToLower () + 
						                         "\\>\n[[chaos\n" + csTmp.rawCode + "\nchaos]]\n",
						                         "codesnippet_code");
					csTmp.update ();
				}
			}
			cs_changed = false;
		}

		void OnFocusOut (object sender, Gtk.FocusOutEventArgs args)
		{
			cs_changed = true;
		}
	}
}
