using UnityEngine;
using TMPro;

public class PulseText : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float minScale = 0.9f;
    [SerializeField] private float maxScale = 1.1f;
    
    private TextMeshProUGUI text;
    
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
    }
    
    void Update()
    {
        if (text != null)
        {
            float scale = Mathf.Lerp(minScale, maxScale, 
                (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) / 2f);
            text.transform.localScale = Vector3.one * scale;
        }
    }
}