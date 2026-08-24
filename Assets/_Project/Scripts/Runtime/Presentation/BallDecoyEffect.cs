using PrideCourt.Domain;
using UnityEngine;

namespace PrideCourt.Presentation
{
    /// <summary>
    /// A presentation-only false ball used by Poko. It never owns a collider or rigidbody,
    /// so the deception cannot alter scoring, input, or the authoritative ball state.
    /// </summary>
    public sealed class BallDecoyEffect : MonoBehaviour
    {
        private const float Lifetime = 0.72f;
        private Transform sourceBall;
        private Vector3 lateralDirection;
        private Material material;
        private float elapsed;

        public static void Play(Transform ball, CourtSide sourceSide)
        {
            if (ball == null) return;

            GameObject decoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            decoy.name = "ぽこ・うつし身の偽球";
            decoy.hideFlags = HideFlags.DontSave;
            Collider collider = decoy.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            decoy.transform.position = ball.position;
            decoy.transform.localScale = ball.lossyScale * 1.05f;
            BallDecoyEffect effect = decoy.AddComponent<BallDecoyEffect>();
            effect.Configure(ball, sourceSide);
        }

        private void Configure(Transform ball, CourtSide sourceSide)
        {
            sourceBall = ball;
            lateralDirection = sourceSide == CourtSide.Near ? Vector3.left : Vector3.right;
            material = new Material(Shader.Find("Sprites/Default"))
            {
                name = "ぽこ・偽球マテリアル",
                color = new Color(0.58f, 1f, 0.18f, 0.7f)
            };
            GetComponent<Renderer>().sharedMaterial = material;

            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.28f;
            trail.startWidth = 0.17f;
            trail.endWidth = 0f;
            trail.sharedMaterial = material;
            trail.startColor = new Color(0.65f, 1f, 0.2f, 0.7f);
            trail.endColor = new Color(1f, 0.2f, 0.68f, 0f);
        }

        private void LateUpdate()
        {
            elapsed += Time.deltaTime;
            if (sourceBall == null || elapsed >= Lifetime)
            {
                if (material != null) Destroy(material);
                Destroy(gameObject);
                return;
            }

            float normalized = Mathf.Clamp01(elapsed / Lifetime);
            float separation = Mathf.Sin(normalized * Mathf.PI) * 2.15f;
            transform.position = sourceBall.position + lateralDirection * separation +
                                 Vector3.up * (Mathf.Sin(normalized * Mathf.PI) * 0.28f);
            Color color = material.color;
            color.a = Mathf.Lerp(0.7f, 0f, normalized);
            material.color = color;
        }
    }
}
