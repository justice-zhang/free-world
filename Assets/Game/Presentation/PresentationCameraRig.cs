using UnityEngine;

namespace Game.Presentation
{
    /// <summary>Single camera follower with finite bounds and optional procedural shake.</summary>
    public sealed class PresentationCameraRig : MonoBehaviour
    {
        private Transform target;
        private Rect bounds = new Rect(-1000f, -1000f, 2000f, 2000f);
        private float shakeAmplitude;
        private float shakeRemaining;
        private float shakeDuration;

        public bool EffectsEnabled { get; set; } = true;
        public Vector3 LastStablePosition { get; private set; }

        public void SetTarget(Transform value) => target = value;

        public void SetBounds(Rect value)
        {
            if (value.width <= 0f || value.height <= 0f) return;
            bounds = value;
        }

        public void RequestShake(float amplitude, float duration)
        {
            if (!EffectsEnabled || amplitude <= 0f || duration <= 0f) return;
            shakeAmplitude = Mathf.Max(shakeAmplitude, amplitude);
            shakeRemaining = Mathf.Max(shakeRemaining, duration);
            shakeDuration = Mathf.Max(shakeDuration, duration);
        }

        public void TickCamera(float unscaledDeltaTime)
        {
            var current = transform.position;
            if (target != null)
            {
                current.x = Mathf.Clamp(target.position.x, bounds.xMin, bounds.xMax);
                current.y = Mathf.Clamp(target.position.y, bounds.yMin, bounds.yMax);
            }
            LastStablePosition = new Vector3(current.x, current.y, current.z);
            if (EffectsEnabled && shakeRemaining > 0f)
            {
                shakeRemaining = Mathf.Max(0f, shakeRemaining - unscaledDeltaTime);
                var normalized = shakeDuration > 0f ? shakeRemaining / shakeDuration : 0f;
                var phase = Time.unscaledTime * 47f;
                current.x += Mathf.Sin(phase) * shakeAmplitude * normalized;
                current.y += Mathf.Cos(phase * 1.37f) * shakeAmplitude * normalized;
            }
            transform.position = current;
        }

        private void LateUpdate() => TickCamera(Time.unscaledDeltaTime);
    }
}
