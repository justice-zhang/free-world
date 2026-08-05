using Game.Core;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>Presentation-only Scene binding for one baked stable map anchor.</summary>
    public sealed class MapAnchorBinding : MonoBehaviour
    {
        [SerializeField] private string anchorId = string.Empty;

        public string AnchorIdText => anchorId;

        public bool TryGetAnchorId(out ContentId id)
        {
            var result = ContentId.Create(anchorId);
            id = result.IsSuccess ? result.Value : default;
            return result.IsSuccess;
        }

#if UNITY_EDITOR
        public void Configure(string stableAnchorId)
        {
            anchorId = stableAnchorId ?? string.Empty;
        }
#endif
    }
}
