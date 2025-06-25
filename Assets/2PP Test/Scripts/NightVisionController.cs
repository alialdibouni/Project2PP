using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

[RequireComponent(typeof(Volume))]
public class NightVisionController : MonoBehaviour
{
    [SerializeField] private Color defaultLightColour;
    [SerializeField] private Color boostedLightColour;

    private bool isNightVisionEnabled;

    private Volume volume;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        RenderSettings.ambientLight = defaultLightColour;

        volume = gameObject.GetComponent<Volume>();
        volume.weight = 0;
    }

    // Update is called once per frame
   private void Update()
    {
        if(Input.GetKeyDown(KeyCode.N))
        {
            ToggleNightVision();
        }
    }

    private void ToggleNightVision()
    {
        isNightVisionEnabled = !isNightVisionEnabled;

        if (isNightVisionEnabled)
        {
            RenderSettings.ambientLight = boostedLightColour;
            volume.weight = 1; // Enable night vision effect
        }
        else
        {
            RenderSettings.ambientLight = defaultLightColour;
            volume.weight = 0; // Disable night vision effect
        }
        //throw new NotImplementedException();
    }
}
