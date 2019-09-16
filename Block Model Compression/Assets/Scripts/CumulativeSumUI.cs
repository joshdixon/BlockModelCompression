using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CumulativeSumUI : MonoBehaviour
{
    public VoxelTerrain terrain;

    public ShadedWireframe wireframeFilter;

    private void Start()
    {
        wireframeFilter.enabled = false;
    }

    public void RadiusSliderValueChanged(Slider slider)
    {
        terrain.spheres[0].radius = (int)slider.value;

        terrain.Regenerate();
    }
    public void SphereOffsetSliderValueChanged(Slider slider)
    {
        terrain.spheres[0].center.z = (int)slider.value;

        terrain.Regenerate();
    }

    public void WireFrameToggled(Toggle toggle)
    {
        wireframeFilter.enabled = toggle.isOn;
    }

    public void DebugSumToggled(Toggle toggle)
    {
         terrain.debugUseFinalSum = toggle.isOn;
    }
}
