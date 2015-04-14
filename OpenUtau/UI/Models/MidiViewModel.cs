﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.ComponentModel;

using OpenUtau.Core.USTx;
using OpenUtau.UI.Controls;

namespace OpenUtau.UI.Models
{
    public class MidiViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        public UProject Project;
        public UVoicePart Part;
        public Canvas MidiCanvas;
        public Canvas ExpCanvas;

        protected bool _updated = false;
        public void MarkUpdate() { _updated = true; }

        string _title = "New Part";
        double _trackHeight = UIConstants.NoteDefaultHeight;
        double _quarterCount = UIConstants.MinQuarterCount;
        double _quarterWidth = UIConstants.MidiQuarterDefaultWidth;
        double _viewWidth;
        double _viewHeight;
        double _offsetX = 0;
        double _offsetY = UIConstants.NoteDefaultHeight * 5 * 12;
        double _quarterOffset = 0;
        double _minTickWidth = UIConstants.MidiTickMinWidth;
        int _beatPerBar = 4;
        int _beatUnit = 4;
        int _visualPosTick;
        int _visualDurTick;

        public string Title { set { _title = value; } get { return _title; } }
        public double TotalHeight { get { return UIConstants.MaxNoteNum * _trackHeight - _viewHeight; } }
        public double TotalWidth { get { return _quarterCount * _quarterWidth - _viewWidth; } }
        public double QuarterCount { set { _quarterCount = value; HorizontalPropertiesChanged(); } get { return _quarterCount; } }
        public double TrackHeight
        {
            set
            {
                _trackHeight = Math.Max(ViewHeight / UIConstants.MaxNoteNum, Math.Max(UIConstants.NoteMinHeight, Math.Min(UIConstants.NoteMaxHeight, value)));
                VerticalPropertiesChanged();
            }
            get { return _trackHeight; }
        }

        public double QuarterWidth
        {
            set
            {
                _quarterWidth = Math.Max(ViewWidth / QuarterCount, Math.Max(UIConstants.MidiQuarterMinWidth, Math.Min(UIConstants.MidiQuarterMaxWidth, value)));
                HorizontalPropertiesChanged();
            }
            get { return _quarterWidth; }
        }

        public double ViewWidth { set { _viewWidth = value; HorizontalPropertiesChanged(); QuarterWidth = QuarterWidth; OffsetX = OffsetX; } get { return _viewWidth; } }
        public double ViewHeight { set { _viewHeight = value; VerticalPropertiesChanged(); TrackHeight = TrackHeight; OffsetY = OffsetY; } get { return _viewHeight; } }
        public double OffsetX { set { _offsetX = Math.Min(TotalWidth, Math.Max(0, value)); HorizontalPropertiesChanged(); } get { return _offsetX; } }
        public double OffsetY { set { _offsetY = Math.Min(TotalHeight, Math.Max(0, value)); VerticalPropertiesChanged(); } get { return _offsetY; } }
        public double ViewportSizeX { get { if (TotalWidth < 1) return 10000; else return ViewWidth * (TotalWidth + ViewWidth) / TotalWidth; } }
        public double ViewportSizeY { get { if (TotalHeight < 1) return 10000; else return ViewHeight * (TotalHeight + ViewHeight) / TotalHeight; } }
        public double SmallChangeX { get { return ViewportSizeX / 10; } }
        public double SmallChangeY { get { return ViewportSizeY / 10; } }
        public double QuarterOffset { set { _quarterOffset = value; HorizontalPropertiesChanged(); } get { return _quarterOffset; } }
        public double MinTickWidth { set { _minTickWidth = value; HorizontalPropertiesChanged(); } get { return _minTickWidth; } }
        public int BeatPerBar { set { _beatPerBar = value; HorizontalPropertiesChanged(); } get { return _beatPerBar; } }
        public int BeatUnit { set { _beatUnit = value; HorizontalPropertiesChanged(); } get { return _beatUnit; } }

        public void HorizontalPropertiesChanged()
        {
            OnPropertyChanged("QuarterWidth");
            OnPropertyChanged("TotalWidth");
            OnPropertyChanged("OffsetX");
            OnPropertyChanged("ViewportSizeX");
            OnPropertyChanged("SmallChangeX");
            OnPropertyChanged("QuarterOffset");
            OnPropertyChanged("QuarterCount");
            OnPropertyChanged("MinTickWidth");
            OnPropertyChanged("BeatPerBar");
            OnPropertyChanged("BeatUnit");
            MarkUpdate();
        }

        public void VerticalPropertiesChanged()
        {
            OnPropertyChanged("TrackHeight");
            OnPropertyChanged("TotalHeight");
            OnPropertyChanged("OffsetY");
            OnPropertyChanged("ViewportSizeY");
            OnPropertyChanged("SmallChangeY");
            MarkUpdate();
        }

        public List<UNote> SelectedNotes = new List<UNote>();
        public List<UNote> TempSelectedNotes = new List<UNote>();
        public List<NoteControl> NoteControls = new List<NoteControl>();
        //public List<NoteExpElement> NoteExpElements = new List<NoteExpElement>();
        public NoteExpElement expElement;

        public MidiViewModel()
        {
        }

        public void RedrawIfUpdated()
        {
            if (Part.PosTick != _visualPosTick || Part.DurTick != _visualDurTick) PartUpdated();
            if (_updated)
            {
                foreach (NoteControl noteControl in NoteControls)
                {
                    if (NoteIsInView(noteControl.Note))
                    {
                        noteControl.Width = Math.Max(UIConstants.NoteMinDisplayWidth, QuarterWidth * noteControl.Note.DurTick / Project.Resolution - 1);
                        noteControl.Height = TrackHeight - 2;
                    }
                    Canvas.SetLeft(noteControl, QuarterWidth * noteControl.Note.PosTick / Project.Resolution - OffsetX + 1);
                    Canvas.SetTop(noteControl, NoteNumToCanvas(noteControl.Note.NoteNum) + 1);
                }
                expElement.X = -OffsetX;
                expElement.ScaleX = QuarterWidth / Project.Resolution;
                expElement.VisualHeight = ExpCanvas.ActualHeight;
            }
            _updated = false;
        }

        # region Selection

        public void UpdateSelectedVisual()
        {
            foreach (NoteControl noteControl in NoteControls)
            {
                if (SelectedNotes.Contains(noteControl.Note) || TempSelectedNotes.Contains(noteControl.Note)) noteControl.Selected = true;
                else noteControl.Selected = false;
            }
        }

        public void SelectAll() { SelectedNotes.Clear(); foreach (UNote note in Part.Notes) SelectedNotes.Add(note); UpdateSelectedVisual(); }
        public void DeselectAll() { SelectedNotes.Clear(); UpdateSelectedVisual(); }

        public void SelectNote(UNote note) { if (!SelectedNotes.Contains(note)) SelectedNotes.Add(note); }
        public void DeselectNote(UNote note) { SelectedNotes.Remove(note); }

        public void SelectTempNote(UNote note) { TempSelectedNotes.Add(note); }
        public void TempSelectInBox(double quarter1, double quarter2, int noteNum1, int noteNum2)
        {
            if (quarter2 < quarter1) { double temp = quarter1; quarter1 = quarter2; quarter2 = temp; }
            if (noteNum2 < noteNum1) { int temp = noteNum1; noteNum1 = noteNum2; noteNum2 = temp; }
            int tick1 = (int)(quarter1 * Project.Resolution);
            int tick2 = (int)(quarter2 * Project.Resolution);
            TempSelectedNotes.Clear();
            foreach (UNote note in Part.Notes)
            {
                if (note.PosTick <= tick2 && note.PosTick + note.DurTick >= tick1 &&
                    note.NoteNum >= noteNum1 && note.NoteNum <= noteNum2) SelectTempNote(note);
            }
            UpdateSelectedVisual();
        }

        public void DoneTempSelect()
        {
            foreach (UNote note in TempSelectedNotes) SelectNote(note);
            TempSelectedNotes.Clear();
            UpdateSelectedVisual();
        }

        public void RemoveSelectedNotes()
        {
            foreach (UNote note in SelectedNotes)
            {
                NoteControl nc = GetNoteControl(note);
                Part.Notes.Remove(nc.Note);
                MidiCanvas.Children.Remove(nc);
                NoteControls.Remove(nc);
            }
            SelectedNotes.Clear();
        }

        # endregion

        # region Note operation

        public void UnloadPart()
        {
            if (Part == null || Project == null) return;
            foreach (NoteControl noteControl in NoteControls)
            {
                MidiCanvas.Children.Remove(noteControl);
            }
            SelectedNotes.Clear();
            NoteControls.Clear();

            Title = "Midi Editor";
            Part = null;
            Project = null;

            expElement.Part = null;
        }

        public void LoadPart(UVoicePart part, UProject project)
        {
            if (project == Project && part == Part) return;
            UnloadPart();
            Part = part;
            Project = project;

            foreach (UNote note in part.Notes)
            {
                AddNoteControl(note);
            }
            PartUpdated();

            if (expElement == null)
            {
                expElement = new NoteExpElement() { Key = "velocity" };
                ExpCanvas.Children.Add(expElement);
            }
            expElement.Part = Part;
        }

        public void PartUpdated()
        {
            Title = Part.Name;
            QuarterOffset = (double)Part.PosTick / Project.Resolution;
            QuarterCount = (double)Part.DurTick / Project.Resolution;
            QuarterWidth = QuarterWidth;
            OffsetX = OffsetX;
            MarkUpdate();
            _visualPosTick = Part.PosTick;
            _visualDurTick = Part.DurTick;
        }

        public NoteControl GetNoteControl(UNote note)
        {
            foreach (NoteControl nc in NoteControls) if (nc.Note == note) return nc;
            return null;
        }

        public void AddNote(UNote note)
        {
            Part.Notes.Add(note);
            AddNoteControl(note);
            expElement.Redraw();
        }

        public void AddNoteControl(UNote note)
        {
            NoteControl noteControl = new NoteControl()
            {
                Note = note,
                Channel = note.Channel,
                Lyric = note.Lyric
            };
            MidiCanvas.Children.Add(noteControl);
            NoteControls.Add(noteControl);
        }

        public void RemoveNote(NoteControl nc)
        {
            if (SelectedNotes.Contains(nc.Note)) SelectedNotes.Remove(nc.Note);
            Part.Notes.Remove(nc.Note);
            MidiCanvas.Children.Remove(nc);
            NoteControls.Remove(nc);
            expElement.Redraw();
        }

        public void RemoveNote(UNote note)
        {
            NoteControl nc = GetNoteControl(note);
            RemoveNote(nc);
        }

        # endregion

        # region Calculation

        public double GetSnapUnit() { return OpenUtau.Core.MusicMath.getZoomRatio(QuarterWidth, BeatPerBar, BeatUnit, MinTickWidth); }
        public double CanvasToQuarter(double X) { return (X + OffsetX) / QuarterWidth; }
        public double QuarterToCanvas(double X) { return X * QuarterWidth - OffsetX; }
        public double CanvasToSnappedQuarter(double X)
        {
            double quater = CanvasToQuarter(X);
            double snapUnit = GetSnapUnit();
            return (int)(quater / snapUnit) * snapUnit;
        }
        public double CanvasToNextSnappedQuarter(double X)
        {
            double quater = CanvasToQuarter(X);
            double snapUnit = GetSnapUnit();
            return (int)(quater / snapUnit) * snapUnit + snapUnit;
        }
        public double CanvasRoundToSnappedQuarter(double X)
        {
            double quater = CanvasToQuarter(X);
            double snapUnit = GetSnapUnit();
            return Math.Round(quater / snapUnit) * snapUnit;
        }
        public int CanvasToSnappedTick(double X) { return (int)(CanvasToSnappedQuarter(X) * Project.Resolution); }

        public int CanvasToNoteNum(double Y) { return UIConstants.MaxNoteNum - 1 - (int)((Y + OffsetY) / TrackHeight); }
        public double NoteNumToCanvas(int noteNum) { return TrackHeight * (UIConstants.MaxNoteNum - 1 - noteNum) - OffsetY; }

        public bool NoteIsInView(UNote note) // FIXME : improve performance
        {
            return (double)note.PosTick / Project.Resolution * QuarterWidth < OffsetX + ViewWidth &&
                (double)note.EndTick / Project.Resolution * QuarterWidth > OffsetX;
        }

        public UNote CanvasXToNote(double X)
        {
            int tick = (int)(CanvasToQuarter(X) * Project.Resolution);
            foreach (UNote note in Part.Notes)
            {
                if (note.PosTick <= tick && note.EndTick >= tick) return note;
            }
            return null;
        }

        # endregion
    }
}
