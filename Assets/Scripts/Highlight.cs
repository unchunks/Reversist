using UnityEngine;

public class Highlight : MonoBehaviour
{
    [SerializeField] private Color normalColor;

    [SerializeField] private Color focusedColor;

    private Material _material;

    private void Start()
    {
        _material = GetComponent<MeshRenderer>().material;
        _material.color = normalColor;
        transform.position += new Vector3(0, -0.044f, 0);
    }

    private void OnMouseEnter()
    {
        _material.color = focusedColor;
    }

    private void OnMouseExit()
    {
        _material.color = normalColor;
    }

    private void OnDestroy()
    {
        Destroy(_material);
    }
}
