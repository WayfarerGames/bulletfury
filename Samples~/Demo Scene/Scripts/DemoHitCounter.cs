using BulletFury;
using BulletFury.Data;
using TMPro;
using UnityEngine;

namespace BulletFury.Samples
{
    [DisallowMultipleComponent]
    public sealed class DemoHitCounter : MonoBehaviour, IBulletHitHandler
    {
        [SerializeField] private TextMeshProUGUI worldText;
        [SerializeField] private string textFormat = "Hits: {0}";

        private int _hitCount;

        public int HitCount => _hitCount;

        private void Reset()
        {
            if (worldText != null)
                return;

            var textObject = new GameObject("HitCounterText");
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        }

        private void Awake()
        {
            RefreshText();
        }

        public void Hit(BulletContainer bullet)
        {
            _hitCount++;
            RefreshText();
        }

        private void RefreshText()
        {
            if (worldText == null)
                return;

            worldText.text = string.Format(textFormat, _hitCount);
        }
    }
}
