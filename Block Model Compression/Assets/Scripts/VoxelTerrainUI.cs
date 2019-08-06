using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VoxelTerrainUI : MonoBehaviour
{
    public VoxelTerrain terrain;

    public Slider minFilterSlider;
    public Slider maxFilterSlider;

    public ShadedWireframe wireframeFilter;

    private void Start()
    {
        wireframeFilter.enabled = false;
    }

    public void MinFilterSliderValueChanged(Slider slider)
    {
        terrain.filterLayerMin = (int)slider.value;

        if (terrain.filterLayerMin > terrain.filterLayerMax)
        {
            terrain.filterLayerMax = terrain.filterLayerMin;
            maxFilterSlider.value = terrain.filterLayerMax;
        }

        terrain.GenerateMesh();
    }
    public void MaxFilterSliderValueChanged(Slider slider)
    {
        terrain.filterLayerMax = (int)slider.value;

        if (terrain.filterLayerMax < terrain.filterLayerMin)
        {
            terrain.filterLayerMin = terrain.filterLayerMax;
            minFilterSlider.value = terrain.filterLayerMin;
        }

        terrain.GenerateMesh();
    }

    public void WireFrameToggled(Toggle toggle)
    {
        wireframeFilter.enabled = toggle.isOn;
    }
}
