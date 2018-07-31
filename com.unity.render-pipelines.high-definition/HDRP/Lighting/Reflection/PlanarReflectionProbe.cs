using UnityEngine.Serialization;
using UnityEngine.Rendering;
using System;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    [ExecuteInEditMode]
    public class PlanarReflectionProbe : HDProbe, ISerializationCallbackReceiver
    {
        const int currentVersion = 2;

        [Serializable]
        public struct PlanarCaptureProperties
        {
            public CapturePositionMode capturePositionMode;
            public Vector3 localReferencePosition;
        }

        public PlanarCaptureProperties probeCaptureProperties;

        public override Hash128 ComputeBakePropertyHashes()
        {
            var capturePropertiesHash = new Hash128();
            HashUtilities.ComputeHash128(ref captureProperties, ref capturePropertiesHash);
            var probeCapturePropertiesHash = new Hash128();
            HashUtilities.ComputeHash128(ref probeCaptureProperties, ref probeCapturePropertiesHash);
            HashUtilities.AppendHash(ref capturePropertiesHash, ref probeCapturePropertiesHash);
            return probeCapturePropertiesHash;
        }

        public override void GetCaptureTransformFor(
            Vector3 viewerPosition, Quaternion viewerRotation,
            out Vector3 capturePosition, out Quaternion captureRotation
        )
        {
            switch (probeCaptureProperties.capturePositionMode)
            {
                case CapturePositionMode.MirrorReference:
                {
                    viewerPosition = probeCaptureProperties.localReferencePosition;
                    viewerRotation = Quaternion.LookRotation(transform.position - viewerPosition);
                    break;
                }
            }

            var reflectionMatrix = GeometryUtils.CalculateReflectionMatrix(
                new Plane(transform.forward, transform.position)
            );
            capturePosition = reflectionMatrix.MultiplyPoint(viewerPosition);
            var forward = reflectionMatrix.MultiplyVector(viewerRotation * Vector3.forward);
            var up = reflectionMatrix.MultiplyVector(viewerRotation * Vector3.up);
            captureRotation = Quaternion.LookRotation(forward, up);
        }

        [SerializeField, FormerlySerializedAs("version")]
        int m_Version;

        public enum CapturePositionMode
        {
            MirrorReference,
            MirrorViewer,
        }

        [SerializeField]
        Vector3 m_CaptureLocalPosition;
        [SerializeField]
        Texture m_CustomTexture;
        [SerializeField]
        Texture m_BakedTexture;
        [SerializeField]
        FrameSettings m_FrameSettings = null;
        [SerializeField]
        float m_CaptureNearPlane = 1;
        [SerializeField]
        float m_CaptureFarPlane = 1000;
        [SerializeField]
        CapturePositionMode m_CapturePositionMode = CapturePositionMode.MirrorReference;
        [SerializeField]
        Vector3 m_CaptureMirrorPlaneLocalPosition;
        [SerializeField]
        Vector3 m_CaptureMirrorPlaneLocalNormal = Vector3.up;
        [SerializeField]
        bool m_OverrideFieldOfView = false;
        [SerializeField]
        [Range(0, 180)]
        float m_FieldOfViewOverride = 90;

        RenderTexture m_RealtimeTexture;

        public bool overrideFieldOfView { get { return m_OverrideFieldOfView; } }
        public float fieldOfViewOverride { get { return m_FieldOfViewOverride; } }

        public BoundingSphere boundingSphere { get { return influenceVolume.GetBoundingSphereAt(transform); } }

        public Texture texture
        {
            get
            {
                switch (captureProperties.mode)
                {
                    default:
                    case HDReflectionProbeMode.Baked:
                        return bakedTexture;
                    case HDReflectionProbeMode.Custom:
                        return customTexture;
                    case HDReflectionProbeMode.Realtime:
                        return realtimeTexture;
                }
            }
        }
        public Bounds bounds { get { return influenceVolume.GetBoundsAt(transform); } }
        public Vector3 captureLocalPosition { get { return m_CaptureLocalPosition; } set { m_CaptureLocalPosition = value; } }
        public Matrix4x4 influenceToWorld
        {
            get
            {
                var tr = transform;
                var influencePosition = influenceVolume.GetWorldPosition(tr);
                return Matrix4x4.TRS(
                    influencePosition,
                    tr.rotation,
                    Vector3.one
                    );
            }
        }
        public Texture customTexture { get { return m_CustomTexture; } set { m_CustomTexture = value; } }
        public RenderTexture realtimeTexture { get { return m_RealtimeTexture; } internal set { m_RealtimeTexture = value; } }
        public FrameSettings frameSettings { get { return m_FrameSettings; } }
        public float captureNearPlane { get { return m_CaptureNearPlane; } }
        public float captureFarPlane { get { return m_CaptureFarPlane; } }
        public CapturePositionMode capturePositionMode { get { return m_CapturePositionMode; } }
        public Vector3 captureMirrorPlaneLocalPosition
        {
            get { return m_CaptureMirrorPlaneLocalPosition; }
            set { m_CaptureMirrorPlaneLocalPosition = value; }
        }
        public Vector3 captureMirrorPlanePosition { get { return transform.TransformPoint(m_CaptureMirrorPlaneLocalPosition); } }
        public Vector3 captureMirrorPlaneLocalNormal
        {
            get { return m_CaptureMirrorPlaneLocalNormal; }
            set { m_CaptureMirrorPlaneLocalNormal = value; }
        }
        public Vector3 captureMirrorPlaneNormal { get { return transform.TransformDirection(m_CaptureMirrorPlaneLocalNormal); } }

        #region Proxy Properties
        public Matrix4x4 proxyToWorld
        {
            get
            {
                return proxyVolume != null
                    ? proxyVolume.transform.localToWorldMatrix
                    : influenceToWorld;
            }
        }
        public ProxyShape proxyShape
        {
            get
            {
                return proxyVolume != null
                    ? proxyVolume.proxyVolume.shape
                    : (ProxyShape)influenceVolume.shape;
            }
        }
        public Vector3 proxyExtents
        {
            get
            {
                return proxyVolume != null
                    ? proxyVolume.proxyVolume.extents
                    : influenceVolume.boxSize;
            }
        }
        public bool infiniteProjection
        {
            get
            {
                return proxyVolume != null
                    && proxyVolume.proxyVolume.shape == ProxyShape.Infinite;
            }
        }

        public bool useMirrorPlane
        {
            get
            {
                return mode == ReflectionProbeMode.Realtime
                    && refreshMode == ReflectionProbeRefreshMode.EveryFrame
                    && capturePositionMode == CapturePositionMode.MirrorViewer;
            }
        }

        #endregion

        public void RequestRealtimeRender()
        {
            if (isActiveAndEnabled)
                ReflectionSystem.RequestRealtimeRender(this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ReflectionSystem.RegisterProbe(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ReflectionSystem.UnregisterProbe(this);
        }

        void OnValidate()
        {
            ReflectionSystem.UnregisterProbe(this);

            if (isActiveAndEnabled)
                ReflectionSystem.RegisterProbe(this);
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (m_Version != currentVersion)
            {
                // Add here data migration code
                if(m_Version < 2)
                {
                    influenceVolume.MigrateOffsetSphere();
                }
                m_Version = currentVersion;
            }
        }
    }
}
