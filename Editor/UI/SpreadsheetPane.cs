using UnityEngine;
using UnityEditor;

namespace Unity.MemoryProfiler.Editor.UI
{
    internal class SpreadsheetPane : ViewPane
    {
        static class Content
        {
            public static readonly GUIContent ExportLabel = new GUIContent("Export Table");
        }

        public string TableDisplayName
        {
            get
            {
                return m_Spreadsheet.SourceTable.GetDisplayName();
            }
        }

        UI.DatabaseSpreadsheet m_Spreadsheet;
        Database.TableReference m_CurrentTableLink;

        public int CurrentTableIndex { get; private set; }

        protected bool m_NeedRefresh = false;

        UnityEngine.UIElements.VisualElement m_ToolbarExtension;
        UnityEngine.UIElements.IMGUIContainer m_ToolbarExtensionPane;
        UIState.BaseMode m_ToolbarExtensionMode;

        internal class History : HistoryEvent
        {
            readonly Database.TableReference m_Table;
            readonly DatabaseSpreadsheet.State m_SpreadsheetState;

            public History(SpreadsheetPane spreadsheetPane, UIState.BaseMode mode, Database.CellLink cell)
            {
                Mode = mode;
                m_Table = spreadsheetPane.m_CurrentTableLink;
                m_SpreadsheetState = spreadsheetPane.m_Spreadsheet.CurrentState;
            }

            public void Restore(SpreadsheetPane pane)
            {
                var table = pane.m_UIState.CurrentMode.GetSchema().GetTableByReference(m_Table);
                if (table == null)
                {
                    Debug.LogError("No table named '" + m_Table.Name + "' found.");
                    return;
                }
                pane.m_CurrentTableLink = m_Table;
                pane.CurrentTableIndex = pane.m_UIState.CurrentMode.GetTableIndex(table);
                pane.m_Spreadsheet = new UI.DatabaseSpreadsheet(pane.m_UIState.FormattingOptions, table, pane, m_SpreadsheetState);
                pane.m_Spreadsheet.onClickLink += pane.OnSpreadsheetClick;
                pane.m_EventListener.OnRepaint();
            }

            public override string ToString()
            {
                string s = Mode.GetSchema().GetDisplayName() + seperator + m_Table.Name;
                if (m_Table.Param != null)
                {
                    s += "(";
                    string sp = "";
                    foreach (var p in m_Table.Param.AllParameters)
                    {
                        if (sp != "")
                        {
                            sp += ", ";
                        }
                        sp += p.Key;
                        sp += "=";
                        sp += p.Value.GetValueString(0, Database.DefaultDataFormatter.Instance);
                    }
                    s += sp + ")";
                }
                return s;
            }

            protected override bool IsEqual(HistoryEvent evt)
            {
                var hEvt = evt as History;
                if (hEvt == null)
                    return false;

                return m_Table == hEvt.m_Table
                    && m_SpreadsheetState.Filter == hEvt.m_SpreadsheetState.Filter
                    && m_SpreadsheetState.FirstVisibleRow == hEvt.m_SpreadsheetState.FirstVisibleRow
                    && m_SpreadsheetState.FirstVisibleRowIndex == hEvt.m_SpreadsheetState.FirstVisibleRowIndex
                    && m_SpreadsheetState.SelectedCell == hEvt.m_SpreadsheetState.SelectedCell
                    && m_SpreadsheetState.SelectedRow == hEvt.m_SpreadsheetState.SelectedRow;
            }
        }

        public SpreadsheetPane(UIState s, IViewPaneEventListener l, UnityEngine.UIElements.VisualElement toolbarExtension)
            : base(s, l)
        {
            m_ToolbarExtension = toolbarExtension;

            if (m_ToolbarExtension != null)
            {
                m_ToolbarExtensionPane = new UnityEngine.UIElements.IMGUIContainer(new System.Action(OnGUIToolbarExtension));
                s.CurrentMode.ViewPaneChanged += OnViewPaneChanged;
                s.ModeChanged += OnModeChanged;
            }
        }

        protected void CloseCurrentTable()
        {
            if (m_Spreadsheet != null)
            {
                if (m_Spreadsheet.SourceTable is Database.ExpandTable)
                {
                    (m_Spreadsheet.SourceTable as Database.ExpandTable).ResetAllGroup();
                }
            }
        }

        public void OpenLinkRequest(Database.LinkRequestTable link)
        {
            var tableRef = new Database.TableReference(link.LinkToOpen.TableName, link.Parameters);
            var table = m_UIState.CurrentMode.GetSchema().GetTableByReference(tableRef);
            if (table == null)
            {
                UnityEngine.Debug.LogError("No table named '" + link.LinkToOpen.TableName + "' found.");
                return;
            }
            OpenLinkRequest(link, tableRef, table);
        }

        public bool OpenLinkRequest(Database.LinkRequestTable link, Database.TableReference tableLink, Database.Table table)
        {
            if (link.LinkToOpen.RowWhere != null && link.LinkToOpen.RowWhere.Count > 0)
            {
                Database.Table filteredTable = table;
                if (table.GetMetaData().defaultFilter != null)
                {
                    filteredTable = table.GetMetaData().defaultFilter.CreateFilter(table);
                }
                var whereUnion = new Database.View.WhereUnion(link.LinkToOpen.RowWhere, null, null, null, null, m_UIState.CurrentMode.GetSchema(), filteredTable, link.SourceView == null ? null : link.SourceView.ExpressionParsingContext);
                long rowToSelect = whereUnion.GetIndexFirstMatch(link.SourceRow);
                if (rowToSelect < 0)
                {
                    Debug.LogWarning("Could not find entry in target table '" + link.LinkToOpen.TableName + "'");
                    return false;
                }

                OpenTable(tableLink, table, new Database.CellPosition(rowToSelect, 0));
            }
            else
            {
                OpenTable(tableLink, table, new Database.CellPosition(0, 0));
            }
            return true;
        }

        void OnSpreadsheetClick(UI.DatabaseSpreadsheet sheet, Database.LinkRequest link, Database.CellPosition pos)
        {
            var hEvent = new History(this, m_UIState.CurrentMode, sheet.DisplayTable.GetLinkTo(pos));
            m_UIState.history.AddEvent(hEvent);
            m_EventListener.OnOpenLink(link);
        }

        public void OpenTable(Database.TableReference tableRef, Database.Table table)
        {
            CloseCurrentTable();
            m_CurrentTableLink = tableRef;
            CurrentTableIndex = m_UIState.CurrentMode.GetTableIndex(table);
            m_Spreadsheet = new UI.DatabaseSpreadsheet(m_UIState.FormattingOptions, table, this);
            m_Spreadsheet.onClickLink += OnSpreadsheetClick;
            m_EventListener.OnRepaint();
        }

        public void OpenTable(Database.TableReference tableRef, Database.Table table, Database.CellPosition pos)
        {
            CloseCurrentTable();
            m_CurrentTableLink = tableRef;
            CurrentTableIndex = m_UIState.CurrentMode.GetTableIndex(table);
            m_Spreadsheet = new UI.DatabaseSpreadsheet(m_UIState.FormattingOptions, table, this);
            m_Spreadsheet.onClickLink += OnSpreadsheetClick;
            m_Spreadsheet.Goto(pos);
            m_EventListener.OnRepaint();
        }

        public void OpenHistoryEvent(History e)
        {
            if (e == null) return;
            e.Restore(this);
        }

        public override UI.HistoryEvent GetCurrentHistoryEvent()
        {
            if (m_Spreadsheet != null && m_CurrentTableLink != null)
            {
                var c = m_Spreadsheet.GetLinkToCurrentSelection();
                if (c == null)
                {
                    c = m_Spreadsheet.GetLinkToFirstVisible();
                }
                if (c != null)
                {
                    var hEvent = new History(this, m_UIState.CurrentMode, c);
                    return hEvent;
                }
            }
            return null;
        }

        private void OnGUI_OptionBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var ff = GUILayout.Toggle(m_UIState.FormattingOptions.ObjectDataFormatter.flattenFields, "Flatten Fields");
            if (m_UIState.FormattingOptions.ObjectDataFormatter.flattenFields != ff)
            {
                m_UIState.FormattingOptions.ObjectDataFormatter.flattenFields = ff;
                if (m_Spreadsheet != null)
                {
                    m_NeedRefresh = true;
                }
            }
            var fsf = GUILayout.Toggle(m_UIState.FormattingOptions.ObjectDataFormatter.flattenStaticFields, "Flatten Static Fields");
            if (m_UIState.FormattingOptions.ObjectDataFormatter.flattenStaticFields != fsf)
            {
                m_UIState.FormattingOptions.ObjectDataFormatter.flattenStaticFields = fsf;
                if (m_Spreadsheet != null)
                {
                    m_NeedRefresh = true;
                }
            }
            var spn = GUILayout.Toggle(m_UIState.FormattingOptions.ObjectDataFormatter.ShowPrettyNames, "Pretty Name");
            if (m_UIState.FormattingOptions.ObjectDataFormatter.ShowPrettyNames != spn)
            {
                m_UIState.FormattingOptions.ObjectDataFormatter.ShowPrettyNames = spn;
                m_EventListener.OnRepaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        public override void OnGUI(Rect r)
        {
            if (Event.current.type == EventType.Layout)
            {
                if (m_NeedRefresh)
                {
                    m_Spreadsheet.UpdateTable();
                    m_NeedRefresh = false;
                }
            }
            m_UIState.FormattingOptions.ObjectDataFormatter.forceLinkAllObject = false;
            if (m_Spreadsheet != null)
            {
                GUILayout.BeginArea(r);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Filters:");

                m_Spreadsheet.OnGui_Filters();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(2);
                m_Spreadsheet.OnGUI(r.width);
                GUILayout.Space(2);
                EditorGUILayout.EndHorizontal();

                OnGUI_OptionBar();
                GUILayout.Space(2);
                EditorGUILayout.EndVertical();
                GUILayout.EndArea();
                if (m_NeedRefresh)
                {
                    m_EventListener.OnRepaint();
                }
            }
        }

        public override void OnClose()
        {
            MemoryProfilerAnalytics.SendPendingFilterChanges();
            CloseCurrentTable();
            m_Spreadsheet = null;

            if (m_ToolbarExtensionMode != null)
                m_ToolbarExtensionMode.ViewPaneChanged -= OnViewPaneChanged;

            m_ToolbarExtensionMode = null;
        }

        private void OnModeChanged(UIState.BaseMode newMode, UIState.ViewMode newViewMode)
        {
            if (m_ToolbarExtension == null)
            {
                return;
            }

            if (m_ToolbarExtensionMode != null)
            {
                m_ToolbarExtensionMode.ViewPaneChanged -= OnViewPaneChanged;
                m_ToolbarExtensionMode = null;
            }

            if (newMode != null)
            {
                newMode.ViewPaneChanged += OnViewPaneChanged;
                m_ToolbarExtensionMode = newMode;
            }

            OnViewPaneChanged(newMode.CurrentViewPane);
        }

        private void OnViewPaneChanged(ViewPane newPane)
        {
            if (m_ToolbarExtension == null)
            {
                return;
            }

            if (m_ToolbarExtension.IndexOf(m_ToolbarExtensionPane) != -1)
            {
                m_ToolbarExtension.Remove(m_ToolbarExtensionPane);
            }

            if (newPane == this)
            {
                m_ToolbarExtension.Add(m_ToolbarExtensionPane);
            }
        }

        private void OnGUIToolbarExtension()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var popupRect = GUILayoutUtility.GetRect(Content.ExportLabel, EditorStyles.toolbarPopup);

            if (EditorGUI.DropdownButton(popupRect, Content.ExportLabel, FocusType.Passive, EditorStyles.toolbarButton))
            {
                ExportTableToCSV();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ExportTableToCSV()
        {
            // Export Table To CSV File...      
            var filePath = UnityEditor.EditorUtility.SaveFilePanel("Save current memory table to a csv file", "", "MemorySnapshot.csv", "csv");

            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var table = m_Spreadsheet.DisplayTable;
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
                formatters[col] = m_UIState.FormattingOptions.GetFormatter(metaColumns[col].FormatName);

                if (formatters[col] is Database.SizeDataFormatter)
                {
                    formatters[col] = Database.DefaultDataFormatter.Instance;
                }

                header += table.GetMetaColumnByColumn(columns[col]).DisplayName;
            }

            header += "\n";

            // Export Rows by using threads...
            var rowCount = table.GetRowCount();
            var batchCount = 100; // If you set this value to rowCount, then you can get a single csv file. However it will take enormous amount of time...might be more than 1 hour in empty scene...
            s_ExportProgressCurrent = 0L;
            s_ExportProgressTotal = rowCount;
            ProgressBarDisplay.ShowBar("Exporting all snapshot result...");

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
                while (true)
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

        private static System.Collections.Generic.Queue<ExportWorkItem> s_ExportWorkItemQueue = new System.Collections.Generic.Queue<ExportWorkItem>();
        private static System.Collections.Generic.List<ExportWorkItem> s_ExportToFileItemList = new System.Collections.Generic.List<ExportWorkItem>();
        private static long s_ExportProgressCurrent = 0L;
        private static long s_ExportProgressTotal = 0L;

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
                        str = str.Replace("\"", "\'");

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
    }
}
