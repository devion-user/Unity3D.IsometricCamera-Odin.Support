using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace IsometricCameraModule
{
    [RequireComponent(typeof(Camera))]
    [HideMonoScript]
    public class IsometricCamera : SerializedMonoBehaviour
    {
        [LabelText("Скорость прокрутки")]
        [SuffixLabel("units", true)]
        [SerializeField]
        private float scrollSpeed = 10;

        [HideInInspector, SerializeField]
        private Rect worldRect = new Rect(0, 0, 100, 100);

        public Rect WorldRect
        {
            get { return worldRect; }
            set { worldRect = value; }
        }
        public float ScrollSpeed
        {
            get { return scrollSpeed; }
            set { scrollSpeed = value; }
        }
        public bool SupportHorizontal
        {
            get { return m_Horizontal; }
            set { m_Horizontal = value; }
        }
        public bool SupportVertical
        {
            get { return m_Vertical; }
            set { m_Vertical = value; }
        }

        [BoxGroup("Поддержка осей"), LabelText("Горизонталь")]
        [SerializeField]
        bool m_Horizontal = true;
        [BoxGroup("Поддержка осей"), LabelText("Вертикаль")]
        [SerializeField]
        bool m_Vertical = true;

        [BoxGroup("Инерция"), LabelText("Включено")]
        [SerializeField]
        private bool m_Inertia = true;

        [BoxGroup("Инерция"), LabelText("Скорость торможения"), EnableIf("m_Inertia")]
        [SerializeField]
        private float m_DecelerationRate = .135f;


        [BoxGroup("Эластичность"), LabelText("Включено")]
        [SerializeField]
        private bool m_Elastic = true;
        private bool m_Clamped { get { return !m_Elastic; } }
        [BoxGroup("Эластичность"), LabelText("Восстановление"), Range(0f, 1f), EnableIf("m_Elastic")]
        [SerializeField]
        private float m_Elasticity;
        [BoxGroup("Эластичность"), LabelText("Сопротивление"), Range(0f, 1f), EnableIf("m_Elastic")]
        [SerializeField]
        private float m_ElasticForce = 0.550000011920929f;

        private bool m_Dragging;
        private Vector2 m_Velocity;
        private Camera m_Camera;
        private Bounds m_ViewBounds;
        private Bounds m_ContentBounds;
        private Vector2 m_PrevPosition = Vector2.zero;
        private Bounds m_PrevContentBounds;
        private Bounds m_PrevViewBounds;
        
        public Vector2 Position
        {
            get { return transform.position; }
            set { transform.position = new Vector3(value.x, value.y, transform.position.z); }
        }

        void Awake()
        {
            m_Camera = GetComponent<Camera>();
        }

        void Update()
        {
#if TEST
            if (Input.GetMouseButtonDown(0))
            {
                BeginDragPotentialInitialize();
                BeginDrag(Input.mousePosition);
            }
            if (Input.GetMouseButton(0))
            {
                Drag(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(0))
            {
                EndDrag();
            }
#endif
        }

        void LateUpdate()
        {
            UpdateBounds();
            float unscaledDeltaTime = Time.unscaledDeltaTime;
            Vector2 offset = CalculateOffset(Vector2.zero);
            if (!m_Dragging && (offset != Vector2.zero || m_Velocity != Vector2.zero))
            {
                Vector2 anchoredPosition = Position;
                for (int index = 0; index < 2; ++index)
                {
                    if (m_Elastic && Math.Abs(offset[index]) > .00001f)
                    {
                        float currentVelocity = m_Velocity[index];
                        var axePosition = Position[index];
                        anchoredPosition[index] = Mathf.SmoothDamp(axePosition, axePosition + offset[index], ref currentVelocity, m_Elasticity, float.PositiveInfinity, unscaledDeltaTime);
                        if (Mathf.Abs(currentVelocity) < 1.0)
                            currentVelocity = 0.0f;
                        
                        m_Velocity[index] = currentVelocity;
                    }
                    else if (m_Inertia)
                    {
                        m_Velocity[index] *= Mathf.Pow(m_DecelerationRate, unscaledDeltaTime);
                        if (Mathf.Abs(m_Velocity[index]) < 1.0)
                            m_Velocity[index] = 0.0f;
                        anchoredPosition[index] += m_Velocity[index]*unscaledDeltaTime;
                    }
                    else
                    {
                        m_Velocity[index] = 0.0f;
                    }
                }
                if (m_Velocity != Vector2.zero)
                {
                    if (m_Clamped)
                    {
                        offset = CalculateOffset(anchoredPosition - Position);
                        anchoredPosition += offset;
                    }
                    SetContentAnchoredPosition(anchoredPosition);
                }
            }
            if (m_Dragging && m_Inertia)
            {
                m_Velocity = Vector3.Lerp(m_Velocity, (Position - m_PrevPosition) / unscaledDeltaTime, unscaledDeltaTime * 10f);
            }
            if (m_ViewBounds == m_PrevViewBounds && m_ContentBounds == m_PrevContentBounds && Position == m_PrevPosition)
                return;
            UpdatePrevData();
        }
        protected void UpdatePrevData()
        {
            m_PrevPosition = Position;
            m_PrevViewBounds = m_ViewBounds;
            m_PrevContentBounds = m_ContentBounds;
        }
        protected virtual void SetContentAnchoredPosition(Vector2 position)
        {
            if (!m_Horizontal)
                position.x = Position.x;
            if (!m_Vertical)
                position.y = Position.y;
            if (position == Position)
                return;
            Position = position;
            UpdateBounds();
        }
        protected void UpdateBounds()
        {
            m_ViewBounds = CalculateViewBounds();
            m_ContentBounds = CalculateContentBounds();
            
            Vector3 size = m_ContentBounds.size;
            Vector3 center = m_ContentBounds.center;
            Vector2 pivot = new Vector2(.5f, .5f);
            AdjustBounds(ref m_ViewBounds, ref pivot, ref size, ref center);
            m_ContentBounds.size = size;
            m_ContentBounds.center = center;
            if (!m_Clamped)
                return;
            Vector2 zero = Vector2.zero;
            if (m_ViewBounds.max.x > (double)m_ContentBounds.max.x)
                zero.x = Math.Min(m_ViewBounds.min.x - m_ContentBounds.min.x, m_ViewBounds.max.x - m_ContentBounds.max.x);
            else if (m_ViewBounds.min.x < (double)m_ContentBounds.min.x)
                zero.x = Math.Max(m_ViewBounds.min.x - m_ContentBounds.min.x, m_ViewBounds.max.x - m_ContentBounds.max.x);
            if (m_ViewBounds.min.y < (double)m_ContentBounds.min.y)
                zero.y = Math.Max(m_ViewBounds.min.y - m_ContentBounds.min.y, m_ViewBounds.max.y - m_ContentBounds.max.y);
            else if (m_ViewBounds.max.y > (double)m_ContentBounds.max.y)
                zero.y = Math.Min(m_ViewBounds.min.y - m_ContentBounds.min.y, m_ViewBounds.max.y - m_ContentBounds.max.y);
            if (zero.sqrMagnitude > 1.40129846432482E-45)
            {
                Vector3 contentPos = Position + zero;
                if (!m_Horizontal)
                    contentPos.x = Position.x;
                if (!m_Vertical)
                    contentPos.y = Position.y;
                AdjustBounds(ref m_ViewBounds, ref pivot, ref size, ref contentPos);
            }
        }
        internal static void AdjustBounds(ref Bounds viewBounds, ref Vector2 contentPivot, ref Vector3 contentSize, ref Vector3 contentPos)
        {
            Vector3 vector3 = viewBounds.size - contentSize;
            if (vector3.x > 0.0)
            {
                contentPos.x -= vector3.x * (contentPivot.x - 0.5f);
                contentSize.x = viewBounds.size.x;
            }
            if (vector3.y <= 0.0)
                return;
            contentPos.y -= vector3.y * (contentPivot.y - 0.5f);
            contentSize.y = viewBounds.size.y;
        }

        private Vector2 CalculateOffset(Vector2 delta)
        {
//            var bounds = new Bounds(worldRect.center, worldRect.size);
            return InternalCalculateOffset(ref m_ViewBounds, ref m_ContentBounds, m_Horizontal, m_Vertical, ref delta);
        }

        internal static Vector2 InternalCalculateOffset(ref Bounds viewBounds, ref Bounds contentBounds, bool horizontal, bool vertical, ref Vector2 delta)
        {
            Vector2 zero = Vector2.zero;
            Vector2 minContentBound = contentBounds.min;
            Vector2 maxContentBound = contentBounds.max;
            Vector2 minViewBound = viewBounds.min;
            Vector2 maxViewBound = viewBounds.max;

            for (int i = 0; i < 2; i++)
            {
                var isEnabled = i == 0 ? horizontal : vertical;
                if (isEnabled)
                {
                    minContentBound[i] -= delta[i];
                    maxContentBound[i] -= delta[i];
                    minViewBound[i] -= delta[i];
                    maxViewBound[i] -= delta[i];
                    if (minContentBound[i] > (double) viewBounds.min[i])
                        zero[i] = minContentBound[i] - viewBounds.min[i];
                    else if (maxContentBound[i] < (double) viewBounds.max[i])
                        zero[i] = maxContentBound[i] - viewBounds.max[i];
                }
            }
            return zero;
        }


        Bounds CalculateViewBounds()
        {
            var pixelSize = new Vector2(m_Camera.orthographicSize * m_Camera.aspect, m_Camera.orthographicSize) * 2;
            var pixelBounds = new Bounds(new Vector3(m_Camera.transform.position.x, m_Camera.transform.position.y, 0), pixelSize);
            return pixelBounds;
        }

        Bounds CalculateContentBounds()
        {
            return new Bounds(worldRect.center, worldRect.size);
        }

        public void BeginDragPotentialInitialize()
        {
            m_Velocity = new Vector2();
        }

        private Vector2 m_ContentStartPosition;
        private Vector2 m_PointerStartLocalCursor;
        public void BeginDrag(Vector2 mousePos)
        {
            UpdateBounds();
            m_PointerStartLocalCursor = mousePos;
            m_ContentStartPosition = Position;
            m_Dragging = true;
        }

        public void Drag(Vector2 mousePos)
        {
            UpdateBounds();
            var startDelta = m_PointerStartLocalCursor - mousePos; //delta from current to start
            var contentStartDelta = m_ContentStartPosition + startDelta;
            Vector2 offset = CalculateOffset(contentStartDelta - Position);

            Vector2 position = contentStartDelta + offset;
            if (m_Elastic)
            {
                if (offset.x != 0.0)
                    position.x = position.x - RubberDelta(offset.x, m_ViewBounds.size.x);
                if (offset.y != 0.0)
                    position.y = position.y - RubberDelta(offset.y, m_ViewBounds.size.y);
            }
            SetContentAnchoredPosition(position);
        }

        public float RubberDelta(float overStretching, float viewSize)
        {
            return (float)(1.0 - 1.0 / (Mathf.Abs(overStretching) * m_ElasticForce / viewSize + 1.0)) * viewSize * Mathf.Sign(overStretching);
        }

        public void EndDrag()
        {
            m_Dragging = false;
        }
    }
}