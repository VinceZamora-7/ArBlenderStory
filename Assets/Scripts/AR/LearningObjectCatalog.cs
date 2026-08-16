using System;
using UnityEngine;

namespace ARLearning.AR
{
    public sealed class LearningObjectCatalog : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string Name;
            public GameObject Prefab;
        }

        [SerializeField] Entry[] m_Entries = Array.Empty<Entry>();
        [SerializeField] int m_CurrentIndex;

        public GameObject CurrentPrefab => m_Entries.Length == 0 ? null : m_Entries[m_CurrentIndex].Prefab;
        public string CurrentName => m_Entries.Length == 0 ? "No model" : m_Entries[m_CurrentIndex].Name;
        public event Action<GameObject> Changed;

        public void Configure(Entry[] entries)
        {
            m_Entries = entries;
            m_CurrentIndex = Mathf.Clamp(m_CurrentIndex, 0, Mathf.Max(0, m_Entries.Length - 1));
        }

        public void Next() => Select(m_CurrentIndex + 1);
        public void Previous() => Select(m_CurrentIndex - 1);

        void Select(int index)
        {
            if (m_Entries.Length == 0) return;
            m_CurrentIndex = (index % m_Entries.Length + m_Entries.Length) % m_Entries.Length;
            Changed?.Invoke(CurrentPrefab);
        }
    }
}
