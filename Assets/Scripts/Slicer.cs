using UnityEngine;

public class Slicer:MonoBehaviour
{
    private void OnCollisionEnter(Collision other)
    {
        var sliceableItem = other.gameObject.GetComponent<SliceableItem>();
        if (sliceableItem == null)
        {
            return;
        }
        gameObject.SetActive(false);
        var intensity = other.impulse.magnitude;
        Debug.LogError($"Intensity: {intensity}");
        var sliceCount = CalculateSlicesFromIntensity(intensity);
        if (sliceCount > 0)
        {
            sliceableItem.SliceIntoPieces(other.gameObject, sliceCount);
        }
    }

    private int CalculateSlicesFromIntensity(float intensity)
    {
        return intensity switch
        {
            < 5f => 2,
            < 15f => 5,
            < 30f => 7,
            _ => 9
        };
    }
}