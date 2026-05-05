using UnityEngine;
using System.Collections;

public abstract class DialogAnimationSO : ScriptableObject
{
    public float bgAlpha;
    public abstract IEnumerator PlayIn(B_BaseDialog dialog);
    public abstract IEnumerator PlayOut(B_BaseDialog dialog);
}