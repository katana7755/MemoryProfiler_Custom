namespace Unity.MemoryProfiler.Editor
{
    internal class ObjectAllManagedTable : ObjectListTable
    {
        public new const string TableName = "AllManagedObjects";
        public new const string TableDisplayName = "All Managed Objects";
        private ObjectData[] m_cache;
        public ObjectAllManagedTable(Database.Schema schema, SnapshotObjectDataFormatter formatter, CachedSnapshot snapshot, ManagedData crawledData, ObjectMetaType metaType)
            : base(schema, formatter, snapshot, crawledData, metaType)
        {
            InitObjectList();
        }

        public override string GetName()
        {
            return TableName;
        }

        public override string GetDisplayName()
        {
            return TableDisplayName;
        }

        public override long GetObjectCount()
        {
            return CrawledData.ManagedObjects.Count;
        }

        public override ObjectData GetObjectData(long row)
        {
            if (m_cache == null)
            {
                m_cache = new ObjectData[CrawledData.ManagedObjects.Count];
            }

            if (row < 0 || row >= CrawledData.ManagedObjects.Count)
            {
                UnityEngine.Debug.Log("GetObjectData out of range");
            }
            if (!m_cache[row].IsValid)
            {
                var mo = CrawledData.ManagedObjects[(int)row];
                m_cache[row] = ObjectData.FromManagedPointer(Snapshot, mo.PtrObject);
            }
            return m_cache[row];
        }

        public override bool GetObjectStatic(long row)
        {
            return false;
        }

        public override void EndUpdate(IUpdater updater)
        {
            base.EndUpdate(updater);
            m_cache = null;
        }
    }
    internal class ObjectAllNativeTable : ObjectListTable
    {
        public new const string TableName = "AllNativeObjects";
        public new const string TableDisplayName = "All Native Objects";
        private ObjectData[] m_cache;
        public ObjectAllNativeTable(Database.Schema schema, SnapshotObjectDataFormatter formatter, CachedSnapshot snapshot, ManagedData crawledData, ObjectMetaType metaType)
            : base(schema, formatter, snapshot, crawledData, metaType)
        {
            InitObjectList();
        }

        public override string GetName()
        {
            return TableName;
        }

        public override string GetDisplayName()
        {
            return TableDisplayName;
        }

        public override long GetObjectCount()
        {
            return Snapshot.nativeObjects.Count;
        }

        public override ObjectData GetObjectData(long row)
        {
            if (m_cache == null)
            {
                m_cache = new ObjectData[Snapshot.nativeObjects.Count];
            }
            if (!m_cache[row].IsValid)
            {
                m_cache[row] = ObjectData.FromNativeObjectIndex(Snapshot, (int)row);
            }
            return m_cache[row];
        }

        public override bool GetObjectStatic(long row)
        {
            return false;
        }

        public override void EndUpdate(IUpdater updater)
        {
            base.EndUpdate(updater);
            m_cache = null;
        }
    }
    internal class ObjectAllTable : ObjectListTable
    {
        public new const string TableName = "AllObjects";
        public new const string TableDisplayName = "All Objects";
        private ObjectData[] m_cache;
        public ObjectAllTable(Database.Schema schema, SnapshotObjectDataFormatter formatter, CachedSnapshot snapshot, ManagedData crawledData, ObjectMetaType metaType)
            : base(schema, formatter, snapshot, crawledData, metaType)
        {
            InitObjectList();
        }

        public override string GetName()
        {
            return TableName;
        }

        public override string GetDisplayName()
        {
            return TableDisplayName;
        }

        public override long GetObjectCount()
        {
            return Snapshot.nativeObjects.Count + CrawledData.ManagedObjects.Count;
        }

        public override ObjectData GetObjectData(long row)
        {
            if (m_cache == null)
            {
                m_cache = new ObjectData[Snapshot.nativeObjects.Count + CrawledData.ManagedObjects.Count];
            }
            if (!m_cache[row].IsValid)
            {
                var iNative = Snapshot.UnifiedObjectIndexToNativeObjectIndex((int)row);
                if (iNative >= 0)
                {
                    m_cache[row] = ObjectData.FromNativeObjectIndex(Snapshot, iNative);
                }
                var iManaged = Snapshot.UnifiedObjectIndexToManagedObjectIndex((int)row);
                if (iManaged >= 0)
                {
                    m_cache[row] = ObjectData.FromManagedObjectIndex(Snapshot, iManaged);
                }
            }
            return m_cache[row];
        }

        public override bool GetObjectStatic(long row)
        {
            return false;
        }

        public override void EndUpdate(IUpdater updater)
        {
            base.EndUpdate(updater);
            m_cache = null;
        }
    }

    internal class ObjectPossibleDuplicationTable : ObjectListTable
    {        
        public new const string TableName = "AllPossibleDuplications";
        public new const string TableDisplayName = "All Possible Duplications";
        private ObjectData[] m_cache;

        public ObjectPossibleDuplicationTable(Database.Schema schema, SnapshotObjectDataFormatter formatter, CachedSnapshot snapshot, ManagedData crawledData, ObjectMetaType metaType)
            : base(schema, formatter, snapshot, crawledData, metaType)
        {
            GatherAllPossibleDuplications();
            InitObjectList();
        }

        public override string GetName()
        {
            return TableName;
        }

        public override string GetDisplayName()
        {
            return TableDisplayName;
        }

        public override long GetObjectCount()
        {
            return (m_cache != null) ? m_cache.Length : 0;
        }

        public override ObjectData GetObjectData(long row)
        {
            if (m_cache == null)
            {
                GatherAllPossibleDuplications();
            }

            return m_cache[row];
        }

        public override bool GetObjectStatic(long row)
        {
            return false;
        }

        public override void EndUpdate(IUpdater updater)
        {
            base.EndUpdate(updater);
            m_cache = null;
        }

        private void GatherAllPossibleDuplications()
        {          
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ObjectData>> countDictionary = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ObjectData>>();
            var row = 0;

            while (true)
            {
                var iNative = Snapshot.UnifiedObjectIndexToNativeObjectIndex((int)row);

                if (iNative < 0)
                {
                    break;
                }

                var objectData = ObjectData.FromNativeObjectIndex(Snapshot, iNative);
                var objectKey = GenerateObjectKey(objectData);

                if (!countDictionary.ContainsKey(objectKey))
                {
                    countDictionary[objectKey] = new System.Collections.Generic.List<ObjectData>();
                }

                countDictionary[objectKey].Add(objectData);
                ++row;
            }

            row = 0;

            while (true)
            {
                var iManaged = Snapshot.UnifiedObjectIndexToManagedObjectIndex((int)row);

                if (iManaged < 0)
                {
                    break;
                }

                var objectData = ObjectData.FromManagedObjectIndex(Snapshot, iManaged);
                var objectKey = GenerateObjectKey(objectData);

                if (!countDictionary.ContainsKey(objectKey))
                {
                    countDictionary[objectKey] = new System.Collections.Generic.List<ObjectData>();
                }

                countDictionary[objectKey].Add(objectData);
                ++row;
            }

            var total = 0;

            foreach (var pair in countDictionary)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                total += pair.Value.Count;
            }

            var tempList = new System.Collections.Generic.List<ObjectData>(total);

            foreach (var pair in countDictionary)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                tempList.AddRange(pair.Value);
            }

            m_cache = tempList.ToArray();  
        }

        private string GenerateObjectKey(ObjectData objectData)
        {
            return $"{GetObjectNameString(objectData)},{GetObjectTypeString(objectData)},{GetObjectOwnedSize(objectData)},{GetObjectTargetSize(objectData)}{GetObjectNativeObjectSize(objectData)}";
        }

        private string GetObjectNameString(ObjectData objectData)
        {
            switch (objectData.dataType)
            {
                case ObjectDataType.Array:
                case ObjectDataType.ReferenceArray:
                case ObjectDataType.BoxedValue:
                case ObjectDataType.Value:
                    return string.Empty;
                case ObjectDataType.Object:
                case ObjectDataType.ReferenceObject:
                    ManagedObjectInfo moi = GetMoiFromObjectData(objectData);
                    if (moi.IsValid() && moi.NativeObjectIndex >= 0)
                        return Snapshot.nativeObjects.objectName[moi.NativeObjectIndex];
                    return string.Empty;
                case ObjectDataType.NativeObject:
                //case ObjectDataType.NativeObjectReference:
                    return Snapshot.nativeObjects.objectName[objectData.nativeObjectIndex];
                case ObjectDataType.Global:
                case ObjectDataType.Type:
                case ObjectDataType.Unknown:
                default:
                    return Formatter.Format(objectData, Database.DefaultDataFormatter.Instance);
            }
        }

        private string GetObjectTypeString(ObjectData objectData)
        {
            var d = objectData.displayObject;
            switch (d.dataType)
            {
                case ObjectDataType.Array:
                case ObjectDataType.BoxedValue:
                case ObjectDataType.Object:
                case ObjectDataType.ReferenceArray:
                case ObjectDataType.Value:
                    if (d.managedTypeIndex < 0) return "<unknown type>";
                    return Snapshot.typeDescriptions.typeDescriptionName[d.managedTypeIndex];

                case ObjectDataType.ReferenceObject:
                {
                    var ptr = d.GetReferencePointer();
                    if (ptr != 0)
                    {
                        var obj = ObjectData.FromManagedPointer(Snapshot, ptr);
                        if (obj.IsValid && obj.managedTypeIndex != d.managedTypeIndex)
                        {
                            return "(" + Snapshot.typeDescriptions.typeDescriptionName[obj.managedTypeIndex] + ") "
                                + Snapshot.typeDescriptions.typeDescriptionName[d.managedTypeIndex];
                        }
                    }
                    return Snapshot.typeDescriptions.typeDescriptionName[d.managedTypeIndex];
                }

                case ObjectDataType.Global:
                    return "Global";
                case ObjectDataType.Type:
                    return "Type";
                case ObjectDataType.NativeObject:
                {
                    int iType = Snapshot.nativeObjects.nativeTypeArrayIndex[d.nativeObjectIndex];
                    return Snapshot.nativeTypes.typeName[iType];
                }
                case ObjectDataType.Unknown:
                default:
                    return "<unknown>";
            }
        }

        private long GetObjectOwnedSize(ObjectData objectData)
        {
            var obj = objectData.displayObject;
            switch (obj.dataType)
            {
                case ObjectDataType.Object:
                case ObjectDataType.BoxedValue:
                case ObjectDataType.Array:
                case ObjectDataType.ReferenceArray:
                case ObjectDataType.ReferenceObject:
                    return obj.GetManagedObject(Snapshot).Size;
                case ObjectDataType.Type:
                case ObjectDataType.Value:
                    return Snapshot.typeDescriptions.size[obj.managedTypeIndex];
                case ObjectDataType.NativeObject:
                    return (long)Snapshot.nativeObjects.size[obj.nativeObjectIndex];
                //case ObjectDataType.NativeObjectReference:
                //    return 0;
                default:
                    return 0;
            }
        }            

        private long GetObjectTargetSize(ObjectData objectData)
        {
            var obj = objectData.displayObject;
            switch (obj.dataType)
            {
                case ObjectDataType.Value:
                    return 0;

                case ObjectDataType.ReferenceArray:
                case ObjectDataType.ReferenceObject:
                {
                    var ptr = obj.GetReferencePointer();
                    if (ptr == 0)
                            return 0;                    
                    return obj.GetManagedObject(Snapshot).Size;
                }
                case ObjectDataType.NativeObject:
                    return 0;
                //case ObjectDataType.NativeObjectReference:
                //    return (long)Snapshot.nativeObjects.size[obj.nativeObjectIndex];
                default:
                    return 0;
            }
        }     

        private long GetObjectNativeObjectSize(ObjectData objectData)
        {
            var obj = objectData.displayObject;
            ManagedObjectInfo moi = GetMoiFromObjectData(obj);
            if (moi.IsValid() && moi.NativeObjectIndex >= 0)
            {
                return (long)Snapshot.nativeObjects.size[moi.NativeObjectIndex];
            }
            return 0;
        }        
    }
}
