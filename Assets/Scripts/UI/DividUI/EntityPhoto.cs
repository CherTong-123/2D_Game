using UnityEngine;

public class EntityPhoto : MonoBehaviour
{
    [SerializeField] string photoName;

    public string PhotoName
    {
        get => string.IsNullOrEmpty(photoName) ? gameObject.name : photoName;
        set => photoName = value;
    }
}
