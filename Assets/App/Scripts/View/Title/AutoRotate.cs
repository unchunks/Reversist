using UnityEngine;

public class AutoRotate : MonoBehaviour
{
    private void Update()
    {
        transform.Rotate(0, 5 * Time.deltaTime, 0);
    }

}
