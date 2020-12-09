using UnityEngine;
using System;
using Unity.MemoryProfiler.Editor.UI;
using UnityEditor.Profiling.Memory.Experimental;
using System.Collections.Generic;

namespace Unity.MemoryProfiler.Editor
{
    internal class OpenSnapshotsManager
    {
        OpenSnapshotsWindow m_OpenSnapshotsPane;

        public enum OpenSnapshotSlot
        {
            First,
            Second,
        }

        [NonSerialized]
        SnapshotFileData First;
        [NonSerialized]
        SnapshotFileData Second;

        UIState m_UIState;

        private UI.ViewPane currentViewPane
        {
            get
            {
                if (m_UIState.CurrentMode == null) return null;
                return m_UIState.CurrentMode.CurrentViewPane;
            }
        }

        public void RegisterUIState(UIState uiState)
        {
            m_UIState = uiState;
            uiState.ModeChanged += OnModeChanged;
            OnModeChanged(uiState.CurrentMode, uiState.CurrentViewMode);
        }

        public OpenSnapshotsWindow InitializeOpenSnapshotsWindow(float initialWidth)
        {
            m_OpenSnapshotsPane = new OpenSnapshotsWindow(initialWidth);

            m_OpenSnapshotsPane.SwapOpenSnapshots += SwapOpenSnapshots;
            m_OpenSnapshotsPane.ShowDiffOfOpenSnapshots += ShowDiffOfOpenSnapshots;
            m_OpenSnapshotsPane.ShowFirstOpenSnapshot += ShowFirstOpenSnapshot;
            m_OpenSnapshotsPane.ShowSecondOpenSnapshot += ShowSecondOpenSnapshot;
            m_OpenSnapshotsPane.ExportDiffResultToCSV += ExportDiffResultToCSV;
            return m_OpenSnapshotsPane;
        }

        public void OpenSnapshot(SnapshotFileData snapshot)
        {
            if (First != null)
            {
                if (Second != null)
                {
                    Second.GuiData.CurrentState = SnapshotFileGUIData.State.Closed;
                    UIElementsHelper.SwitchVisibility(Second.GuiData.dynamicVisualElements.openButton, Second.GuiData.dynamicVisualElements.closeButton);
                }
                Second = First;


                m_OpenSnapshotsPane.SetSnapshotUIData(false, Second.GuiData, false);
                Second.GuiData.CurrentState = SnapshotFileGUIData.State.Open;
            }

            First = snapshot;

            m_OpenSnapshotsPane.SetSnapshotUIData(true, snapshot.GuiData, true);
            First.GuiData.CurrentState = SnapshotFileGUIData.State.InView;

            var loadedPackedSnapshot = snapshot.LoadSnapshot();
            if (loadedPackedSnapshot != null)
            {
                m_UIState.SetFirstSnapshot(loadedPackedSnapshot);
            }
        }

        public bool IsSnapshotOpen(SnapshotFileData snapshot)
        {
            return snapshot == First || snapshot == Second;
        }

        public void CloseCapture(SnapshotFileData snapshot)
        {
            if (snapshot == null)
                return;
            try
            {
                if (Second != null)
                {
                    if (snapshot == Second)
                    {
                        m_UIState.ClearSecondMode();
                        Second.GuiData.CurrentState = SnapshotFileGUIData.State.Closed;
                    }
                    else if (snapshot == First)
                    {
                        m_UIState.ClearFirstMode();
                        if (First != null)
                            First.GuiData.CurrentState = SnapshotFileGUIData.State.Closed;
                        First = Second;
                        m_UIState.SwapLastAndCurrentSnapshot();
                    }
                    else
                    {
                        // The snapshot wasn't open, there is nothing left todo here.
                        return;
                    }
                    UIElementsHelper.SwitchVisibility(snapshot.GuiData.dynamicVisualElements.openButton, snapshot.GuiData.dynamicVisualElements.closeButton);
                    Second = null;
                    m_UIState.CurrentViewMode = UIState.ViewMode.ShowFirst;

                    if (First != null)
                        m_OpenSnapshotsPane.SetSnapshotUIData(true, First.GuiData, true);
                    else
                        m_OpenSnapshotsPane.SetSnapshotUIData(true, null, true);
                    m_OpenSnapshotsPane.SetSnapshotUIData(false, null, false);
                    // With two snapshots open, there could also be a diff to be closed/cleared.
                    m_UIState.ClearDiffMode();
                }
                else
                {
                    if (snapshot == First)
                    {
                        First.GuiData.CurrentState = SnapshotFileGUIData.State.Closed;
                        First = null;
                        m_UIState.ClearAllOpenModes();
                    }
                    else if (snapshot == Second)
                    {
                        Second.GuiData.CurrentState = SnapshotFileGUIData.State.Closed;
                        Second = null;
                        m_UIState.ClearAllOpenModes();
                    }
                    else
                    {
                        // The snapshot wasn't open, there is nothing left todo here.
                        return;
                    }
                    m_OpenSnapshotsPane.SetSnapshotUIData(true, null, false);
                    m_OpenSnapshotsPane.SetSnapshotUIData(false, null, false);
                }
                UIElementsHelper.SwitchVisibility(snapshot.GuiData.dynamicVisualElements.openButton, snapshot.GuiData.dynamicVisualElements.closeButton);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void CloseAllOpenSnapshots()
        {
            if (Second != null)
            {
                CloseCapture(Second);
                Second = null;
            }
            if (First != null)
            {
                CloseCapture(First);
                First = null;
            }
        }

        void OnModeChanged(UIState.BaseMode newMode, UIState.ViewMode newViewMode)
        {
            switch (newViewMode)
            {
                case UIState.ViewMode.ShowDiff:
                    if (m_OpenSnapshotsPane != null)
                        m_OpenSnapshotsPane.FocusDiff();

                    if (First != null)
                        First.GuiData.CurrentState = SnapshotFileGUIData.State.Open;
                    if (Second != null)
                        Second.GuiData.CurrentState = SnapshotFileGUIData.State.Open;
                    break;
                case UIState.ViewMode.ShowFirst:
                    if (m_OpenSnapshotsPane != null)
                        m_OpenSnapshotsPane.FocusFirst();

                    if (First != null)
                        First.GuiData.CurrentState = SnapshotFileGUIData.State.InView;
                    if (Second != null)
                        Second.GuiData.CurrentState = SnapshotFileGUIData.State.Open;
                    break;
                case UIState.ViewMode.ShowSecond:
                    if (m_OpenSnapshotsPane != null)
                        m_OpenSnapshotsPane.FocusSecond();

                    if (First != null)
                        First.GuiData.CurrentState = SnapshotFileGUIData.State.Open;
                    if (Second != null)
                        Second.GuiData.CurrentState = SnapshotFileGUIData.State.InView;
                    break;
                default:
                    break;
            }
        }

        void SwapOpenSnapshots()
        {
            var temp = Second;
            Second = First;
            First = temp;

            m_UIState.SwapLastAndCurrentSnapshot();

            if (First != null)
                m_OpenSnapshotsPane.SetSnapshotUIData(true, First.GuiData, m_UIState.CurrentViewMode == UIState.ViewMode.ShowFirst);
            else
                m_OpenSnapshotsPane.SetSnapshotUIData(true, null, false);

            if (Second != null)
                m_OpenSnapshotsPane.SetSnapshotUIData(false, Second.GuiData, m_UIState.CurrentViewMode == UIState.ViewMode.ShowSecond);
            else
                m_OpenSnapshotsPane.SetSnapshotUIData(false, null, false);
        }

        void ShowDiffOfOpenSnapshots()
        {
            if (m_UIState.diffMode != null)
            {
                SwitchSnapshotMode(UIState.ViewMode.ShowDiff);
            }
            else if (First != null && Second != null)
            {
                try
                {
                    MemoryProfilerAnalytics.StartEvent<MemoryProfilerAnalytics.DiffedSnapshotEvent>();

                    m_UIState.DiffLastAndCurrentSnapshot(First.GuiData.UtcDateTime.CompareTo(Second.GuiData.UtcDateTime) < 0);

                    MemoryProfilerAnalytics.EndEvent(new MemoryProfilerAnalytics.DiffedSnapshotEvent());
                }
                catch (Exception)
                {
                    throw;
                }
            }
            else
            {
                Debug.LogError("No second snapshot opened to diff to!");
            }
        }

        void ShowFirstOpenSnapshot()
        {
            if (First != null)
            {
                SwitchSnapshotMode(UIState.ViewMode.ShowFirst);
            }
        }

        void ShowSecondOpenSnapshot()
        {
            if (Second != null)
            {
                SwitchSnapshotMode(UIState.ViewMode.ShowSecond);
            }
        }

        void ExportDiffResultToCSV()
        {      
            if (m_UIState.diffMode == null && m_UIState.CurrentViewMode != UIState.ViewMode.ShowDiff)
            {
                Debug.LogError("You need to diff snapshots first!");
                return;                
            }

            // Export Diff Result To CSV File...      
            var filePath = UnityEditor.EditorUtility.SaveFilePanel("Save current two memory diff result to a csv file", "", "SnapshotDiff.csv", "csv");

            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var table = m_UIState.diffMode.GetSchema().GetTableByName("Diff_" + ObjectAllTable.TableName, null);
            var header = string.Empty;
            
            // Export Header
            var columnCount = table.GetMetaData().GetColumnCount();
            var metaColumns = new Database.MetaColumn[columnCount];
            var columns = new Database.Column[columnCount];
            var formatters = new Database.IDataFormatter[columnCount];

            for (var col = 0; col < columnCount; ++col)
            {
                if (col != 0)
                {
                    header += ",";
                }

                metaColumns[col] = table.GetMetaData().GetColumnByIndex(col);
                columns[col] = table.GetColumnByIndex(col);
                
                if (col == 12) // Owned Size, Use default formatter because SizeDataFormatter doesn't work with threads...
                {
                    formatters[col] = Database.DefaultDataFormatter.Instance;
                }
                else if (col == 13) // Target Size, Use default formatter because SizeDataFormatter doesn't work with threads...
                {
                    formatters[col] = Database.DefaultDataFormatter.Instance;
                }
                else if (col == 14) // Native Size, Use default formatter because SizeDataFormatter doesn't work with threads...
                {
                    formatters[col] = Database.DefaultDataFormatter.Instance;
                }                
                else
                {
                    formatters[col] = m_UIState.FormattingOptions.GetFormatter(metaColumns[col].FormatName);
                }

                header += table.GetMetaColumnByColumn(columns[col]).DisplayName;
            }

            header += "\n";

            // Export Rows by using threads...
            var rowCount = table.GetRowCount();
            var batchCount = 100; // If you set this value to rowCount, then you can get a single csv file. However it will take enormous amount of time...might be more than 1 hour in empty scene...
            s_ExportProgressCurrent = 0L;
            s_ExportProgressTotal = rowCount;
            ProgressBarDisplay.ShowBar("Exporting all diff result...");
           
            for (var row = 0; row < rowCount; row += batchCount)
            {
                var newItem = new ExportWorkItem();
                newItem.header = (row == 0) ? header : string.Empty;
                newItem.startRow = row;
                newItem.endRow = (long)Mathf.Min(row + batchCount, rowCount);
                newItem.rowCount = rowCount;
                newItem.columnCount = columnCount;
                newItem.metaColumns = metaColumns;
                newItem.columns = columns;
                newItem.formatters = formatters;
                s_ExportWorkItemQueue.Enqueue(newItem);
            }

            // if (s_SamplerExportString == null)
            // {
            //     s_SamplerExportString = UnityEngine.Profiling.CustomSampler.Create("Export String");
            // }

            // if (s_SamplerExportFile == null)
            // {
            //     s_SamplerExportFile = UnityEngine.Profiling.CustomSampler.Create("Export File");
            // }

            var threadCount = SystemInfo.processorCount - 3; // Three for main thread, render thread, and file writer...

            for (var i = 0; i < threadCount; ++i)
            {
                new System.Threading.Thread(ThreadProc_ExportOutputStringWorker).Start();
            }

            new System.Threading.Thread(ThreadProc_ExportToFileWorker).Start(filePath);

            // Wait until all tasks are done
            // Make the main thread work for the tasks...
            ExportWorkItem workItem = null;

            while (s_ExportProgressCurrent < s_ExportProgressTotal)
            {
                ProgressBarDisplay.UpdateProgress(((float)s_ExportProgressCurrent / (float)s_ExportProgressTotal), $"Exporting all diff result...({s_ExportProgressCurrent}/{s_ExportProgressTotal})");
                System.Threading.Thread.Sleep(0);

                lock (s_ExportWorkItemQueue)
                {
                    if (s_ExportWorkItemQueue.Count > 0)
                    {
                        workItem = s_ExportWorkItemQueue.Dequeue();
                    }
                    else
                    {
                        workItem = null;
                    }
                }

                if (workItem == null)
                {
                    break;
                }

                workItem.GenerateOutputString();

                lock (s_ExportToFileItemList)
                {
                    var index = s_ExportToFileItemList.FindIndex(a => a.startRow > workItem.startRow);

                    if (index < 0)
                    {
                        s_ExportToFileItemList.Add(workItem);
                    }
                    else
                    {
                        s_ExportToFileItemList.Insert(index, workItem);
                    }
                }
            }

            ProgressBarDisplay.ClearBar();
        }

        private static void ThreadProc_ExportOutputStringWorker()
        {
            // UnityEngine.Profiling.Profiler.BeginThreadProfiling("ExportOutputStringWorker", "ExportOutputStringWorker");
            ExportWorkItem workItem = null;

            while (true)
            {
                // s_SamplerExportString.Begin();
                lock (s_ExportWorkItemQueue)
                {
                    if (s_ExportWorkItemQueue.Count > 0)
                    {
                        workItem = s_ExportWorkItemQueue.Dequeue();
                    }
                    else
                    {
                        workItem = null;
                    }
                }

                if (workItem == null)
                {
                    // s_SamplerExportString.End();
                    break;
                }

                workItem.GenerateOutputString();

                lock (s_ExportToFileItemList)
                {
                    var index = s_ExportToFileItemList.FindIndex(a => a.startRow > workItem.startRow);

                    if (index < 0)
                    {
                        s_ExportToFileItemList.Add(workItem);
                    }
                    else
                    {
                        s_ExportToFileItemList.Insert(index, workItem);
                    }
                }

                // s_SamplerExportString.End();
                System.Threading.Thread.Sleep(0);
            }
            // UnityEngine.Profiling.Profiler.EndThreadProfiling();
        }

        private static void ThreadProc_ExportToFileWorker(object obj)
        {
            // UnityEngine.Profiling.Profiler.BeginThreadProfiling("ExportToFileWorker", "ExportToFileWorker");
            string filePath = (string)obj;
            ExportWorkItem workItem = null;

            using (System.IO.StreamWriter outputFile = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))            
            {
                while(true)
                {
                    // s_SamplerExportFile.Begin();
                    lock (s_ExportToFileItemList)
                    {
                        if (s_ExportToFileItemList.Count > 0 && s_ExportToFileItemList[0].startRow == s_ExportProgressCurrent)
                        {
                            workItem = s_ExportToFileItemList[0];
                            s_ExportToFileItemList.RemoveAt(0);
                        }
                        else
                        {
                            workItem = null;
                        }
                    }

                    if (workItem == null)   
                    {
                        // s_SamplerExportFile.End();
                        System.Threading.Thread.Sleep(10);
                        continue;
                    }

                    outputFile.Write(workItem.outputString);

                    s_ExportProgressCurrent += (workItem.endRow - workItem.startRow);

                    if (s_ExportProgressCurrent >= s_ExportProgressTotal)
                    {
                        break;
                    }

                    // s_SamplerExportFile.End();
                    System.Threading.Thread.Sleep(0);
                }
            }
            // UnityEngine.Profiling.Profiler.EndThreadProfiling();
        }

        private static Queue<ExportWorkItem> s_ExportWorkItemQueue = new Queue<ExportWorkItem>();
        private static List<ExportWorkItem> s_ExportToFileItemList = new List<ExportWorkItem>();
        private static long s_ExportProgressCurrent = 0L;
        private static long s_ExportProgressTotal = 0L;
        // private static UnityEngine.Profiling.CustomSampler s_SamplerExportString;
        // private static UnityEngine.Profiling.CustomSampler s_SamplerExportFile;

        private class ExportWorkItem
        {
            public string header;
            public long startRow;
            public long endRow;
            public long rowCount;
            public int columnCount;
            public Database.MetaColumn[] metaColumns;
            public Database.Column[] columns;
            public Database.IDataFormatter[] formatters;

            public string outputString;

            public void GenerateOutputString()
            {
                outputString = header;

                for (var row = startRow; row < endRow; ++row)
                {
                    for (var col = 0; col < columnCount; ++col)
                    {
                        if (col != 0)
                        {
                            outputString += ",";
                        }

                        var str = columns[col].GetRowValueString(row, formatters[col]);

                        if (str.Contains(",") || str.Contains("\n"))
                        {
                            outputString += $"\"{str}\"";
                        }
                        else
                        {
                            outputString += str;
                        }                            
                    }

                    outputString += "\n";

                    if ((row - startRow + 1) % 10 == 0)
                    {
                        System.Threading.Thread.Sleep(0);
                    }
                }
            }
        }

        void SwitchSnapshotMode(UIState.ViewMode mode)
        {
            if (m_UIState.CurrentViewMode == mode)
                return;

            var currentViewName = "Unknown";
            if (currentViewPane is UI.TreeMapPane)
            {
                currentViewName = "TreeMap";
            }
            else if (currentViewPane is UI.MemoryMapPane)
            {
                currentViewName = "MemoryMap";
            }
            else if (currentViewPane is UI.SpreadsheetPane)
            {
                currentViewName = (currentViewPane as UI.SpreadsheetPane).TableDisplayName;
            }
            MemoryProfilerAnalytics.StartEvent<MemoryProfilerAnalytics.DiffToggledEvent>();

            var oldMode = m_UIState.CurrentViewMode;

            m_UIState.CurrentViewMode = mode;

            MemoryProfilerAnalytics.EndEvent(new MemoryProfilerAnalytics.DiffToggledEvent()
            {
                show = (int)ConvertUIModeToAnalyticsDiffToggleEventData(m_UIState.CurrentViewMode),
                shown = (int)ConvertUIModeToAnalyticsDiffToggleEventData(oldMode),
                viewName = currentViewName
            });
        }

        void BackToSnapshotDiffView()
        {
            m_UIState.CurrentViewMode = UIState.ViewMode.ShowDiff;
        }

        MemoryProfilerAnalytics.DiffToggledEvent.ShowSnapshot ConvertUIModeToAnalyticsDiffToggleEventData(UIState.ViewMode mode)
        {
            switch (mode)
            {
                case UIState.ViewMode.ShowDiff:
                    return MemoryProfilerAnalytics.DiffToggledEvent.ShowSnapshot.Both;
                case UIState.ViewMode.ShowFirst:
                    return MemoryProfilerAnalytics.DiffToggledEvent.ShowSnapshot.First;
                case UIState.ViewMode.ShowSecond:
                    return MemoryProfilerAnalytics.DiffToggledEvent.ShowSnapshot.Second;
                default:
                    throw new NotImplementedException();
            }
        }

        internal void RefreshOpenSnapshots(SnapshotCollectionEnumerator snaps)
        {
            SnapshotFileGUIData firstGUIData = null, secondGUIData = null;

            snaps.Reset();
            while (snaps.MoveNext())
            {
                if (First == snaps.Current)
                {
                    First = snaps.Current;
                    firstGUIData = First.GuiData;
                    firstGUIData.CurrentState = SnapshotFileGUIData.State.Open;
                }
                else if (Second == snaps.Current)
                {
                    Second = snaps.Current;
                    secondGUIData = Second.GuiData;
                    secondGUIData.CurrentState = SnapshotFileGUIData.State.Open;
                }
            }
            m_OpenSnapshotsPane.RefreshScreenshots(firstGUIData, secondGUIData);
        }
    }
}
