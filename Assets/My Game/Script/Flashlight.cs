using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [Header("Light")]
    public Light spotLight;
    public KeyCode toggleKey = KeyCode.Mouse0;
    public bool startOn = false;

    [Header("Flicker (optional)")]
    public bool enableFlicker = false;
    public float flickerIntensity = 0.3f;
    public float flickerSpeed = 12f;

    private bool isOn;
    private float baseIntensity;

    void Awake()
    {
        if (spotLight == null)
            spotLight = GetComponentInChildren<Light>();

        isOn = startOn;
        if (spotLight != null)
        {
            spotLight.enabled = isOn;
            baseIntensity = spotLight.intensity;
        }
    }

    void Update()
    {
        HandleToggle();
        HandleFlicker();
    }

    void HandleToggle()
    {
        if (Input.GetKeyDown(toggleKey))
            SetOn(!isOn);
    }

    public void SetOn(bool state)
    {
        isOn = state;
        if (spotLight != null)
            spotLight.enabled = isOn;
    }

    public void Toggle()
    {
        SetOn(!isOn);
    }

    public bool IsOn()
    {
        return isOn;
    }

    void HandleFlicker()
    {
        if (spotLight == null || !isOn) return;

        if (enableFlicker)
        {
            float noise = (Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) - 0.5f) * 2f;
            spotLight.intensity = baseIntensity + noise * flickerIntensity;
        }
        else
        {
            spotLight.intensity = baseIntensity;
        }
    }
}