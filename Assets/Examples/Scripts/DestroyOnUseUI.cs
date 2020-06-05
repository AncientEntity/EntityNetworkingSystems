using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyOnUseUI : MonoBehaviour
{
    public void DestroyOnUse(GameObject toBeDestroyed)
    {
        Destroy(toBeDestroyed);
    }
}
