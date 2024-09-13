// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;

namespace Prowl.Runtime
{
    // Taken and modified from Duality's ContentRef.cs
    // https://github.com/AdamsLair/duality/blob/master/Source/Core/Duality/ContentRef.cs

    public struct AssetRef<T> : IAssetRef, ISerializable, IEquatable<AssetRef<T>> where T : EngineObject
    {
        private T? _instance;
        private Guid _assetId = Guid.Empty;

        /// <summary>
        /// The actual <see cref="EngineObject"/>. If currently unavailable, it is loaded and then returned.
        /// Because of that, this Property is only null if the references Resource is missing, invalid, or
        /// this content reference has been explicitly set to null. Never returns disposed Resources.
        /// </summary>
        public T? Res
        {
            get
            {
                if (_instance == null || _instance.IsDestroyed) RetrieveInstance();
                return _instance;
            }
            private set
            {
                _assetId = value?.AssetID ?? Guid.Empty;
                FileID = value?.FileID ?? 0;
                _instance = value;
            }
        }

        /// <summary>
        /// Returns the current reference to the Resource that is stored locally. No attemp is made to load or reload
        /// the Resource if currently unavailable.
        /// </summary>
        public T? ResWeak => _instance == null || _instance.IsDestroyed ? null : _instance;

        /// <summary>
        /// The path where to look for the Resource, if it is currently unavailable.
        /// </summary>
        public Guid AssetID
        {
            get { return _assetId; }
            set
            {
                _assetId = value;
                if (_instance != null && _instance.AssetID != value)
                    _instance = null;
            }
        }

        /// <summary>
        /// The Asset index inside the asset file. 0 is the Main Asset
        /// </summary>
        public ushort FileID { get; set; } = 0;


        /// <summary>
        /// Returns whether this content reference has been explicitly set to null.
        /// </summary>
        public bool IsExplicitNull => _instance == null && _assetId == Guid.Empty;

        /// <summary>
        /// Returns whether this content reference is available in general. This may trigger loading it, if currently unavailable.
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                if (_instance is { IsDestroyed: false }) return true;
                RetrieveInstance();
                return _instance is not null;
            }
        }

        /// <summary>
        /// Returns whether the referenced Resource is currently loaded.
        /// </summary>
        public bool IsLoaded => _instance is { IsDestroyed: false } || Application.AssetProvider.HasAsset(_assetId);

        /// <summary>
        /// Returns whether the Resource has been generated at runtime and cannot be retrieved via content path.
        /// </summary>
        public bool IsRuntimeResource => _instance != null && _assetId == Guid.Empty;

        public string Name =>
            _instance != null
                ? _instance.IsDestroyed ? "DESTROYED_" + _instance.Name : _instance.Name
                : "No Instance";

        public Type InstanceType => typeof(T);

        /// <summary>
        /// Creates a AssetRef pointing to the <see cref="EngineObject"/> at the specified id / using
        /// the specified alias.
        /// </summary>
        /// <param name="id"></param>
        public AssetRef(Guid id)
        {
            _instance = null;
            _assetId = id;
            FileID = 0;
        }

        /// <summary>
        /// Creates a AssetRef pointing to the <see cref="EngineObject"/> at the specified id / using
        /// the specified alias.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fileId"></param>
        public AssetRef(Guid id, ushort fileId)
        {
            _instance = null;
            _assetId = id;
            FileID = fileId;
        }

        /// <summary>
        /// Creates a AssetRef pointing to the specified <see cref="EngineObject"/>.
        /// </summary>
        /// <param name="res">The Resource to reference.</param>
        public AssetRef(T? res)
        {
            _instance = res;
            _assetId = res?.AssetID ?? Guid.Empty;
            FileID = res?.FileID ?? 0;
        }

        public object? GetInstance() => Res;

        public void SetInstance(object? obj)
        {
            if (obj is T res)
                Res = res;
            else
                Res = null;
        }

        /// <summary>
        /// Loads the associated content as if it was accessed now.
        /// You don't usually need to call this method. It is invoked implicitly by trying to
        /// access the <see cref="AssetRef{T}"/>.
        /// </summary>
        public void EnsureLoaded()
        {
            if (_instance == null || _instance.IsDestroyed)
                RetrieveInstance();
        }

        /// <summary>
        /// Discards the resolved content reference cache to allow garbage-collecting the Resource
        /// without losing its reference. Accessing it will result in reloading the Resource.
        /// </summary>
        public void Detach()
        {
            _instance = null;
        }

        private void RetrieveInstance()
        {
            if (_assetId != Guid.Empty)
            {
                if (_instance is null)
                    _instance = (T)Application.AssetProvider.LoadAsset<T>(_assetId, FileID);
                else
                    _instance = (T)Application.AssetProvider.LoadAsset<T>(_instance.AssetID, _instance.FileID);
            }
            else
            {
                _instance = null;
            }
        }

        public override string ToString()
        {
            Type resType = typeof(T);

            char stateChar;
            if (IsRuntimeResource)
                stateChar = 'R';
            else if (IsExplicitNull)
                stateChar = 'N';
            else if (IsLoaded)
                stateChar = 'L';
            else
                stateChar = '_';

            return $"[{stateChar}] {resType.Name}";
        }

        public override bool Equals(object? obj)
        {
            if (obj is AssetRef<T> @ref)
                return this == @ref;
            else
                return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (_assetId != Guid.Empty) return _assetId.GetHashCode() + FileID.GetHashCode();
            else if (_instance != null) return _instance.GetHashCode();
            else return 0;
        }

        public bool Equals(AssetRef<T> other)
        {
            return this == other;
        }

        public static implicit operator AssetRef<T>(T res)
        {
            return new AssetRef<T>(res);
        }

        public static explicit operator T(AssetRef<T> res)
        {
            return res.Res;
        }

        /// <summary>
        /// Compares two AssetRefs for equality.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <remarks>
        /// This is a two-step comparison. First, their actual Resources references are compared.
        /// If they're both not null and equal, true is returned. Otherwise, their AssetID's are compared for equality
        /// </remarks>
        public static bool operator ==(AssetRef<T> first, AssetRef<T> second)
        {
            // Old check, didn't work for XY == null when XY was a Resource created at runtime
            //if (first.instance != null && second.instance != null)
            //    return first.instance == second.instance;
            //else
            //    return first.assetID == second.assetID;

            // Completely identical
            if (first._instance == second._instance && first._assetId == second._assetId)
                return true;
            // Same instances
            else if (first._instance != null && second._instance != null)
                return first._instance == second._instance;
            // Null checks
            else if (first.IsExplicitNull) return second.IsExplicitNull;
            else if (second.IsExplicitNull) return first.IsExplicitNull;
            // Path comparison
            else
            {
                Guid? firstPath = first._instance?.AssetID ?? first._assetId;
                Guid? secondPath = second._instance?.AssetID ?? second._assetId;
                return firstPath == secondPath && first.FileID == second.FileID;
            }
        }

        /// <summary>
        /// Compares two AssetRefs for inequality.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        public static bool operator !=(AssetRef<T> first, AssetRef<T> second) => !(first == second);


        public SerializedProperty Serialize(Serializer.SerializationContext ctx)
        {
            SerializedProperty compoundTag = SerializedProperty.NewCompound();
            compoundTag.Add("AssetID", new SerializedProperty(_assetId.ToString()));
            if (_assetId != Guid.Empty)
                ctx.AddDependency(_assetId);
            if (FileID != 0)
                compoundTag.Add("FileID", new SerializedProperty(FileID));
            if (IsRuntimeResource)
                compoundTag.Add("Instance", Serializer.Serialize(_instance, ctx));
            return compoundTag;
        }

        public void Deserialize(SerializedProperty value, Serializer.SerializationContext ctx)
        {
            _assetId = Guid.Parse(value["AssetID"].StringValue);
            FileID = value.TryGet("FileID", out SerializedProperty fileTag) ? fileTag.UShortValue : (ushort)0;
            if (_assetId == Guid.Empty && value.TryGet("Instance", out SerializedProperty tag))
                _instance = Serializer.Deserialize<T?>(tag, ctx);
        }
    }
}
