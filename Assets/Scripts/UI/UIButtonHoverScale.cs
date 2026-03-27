using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float speed = 14f;

    private Vector3 _baseScale;
    private float _target = 1f;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    private void Update()
    {
        float current = transform.localScale.x / (_baseScale.x == 0 ? 1f : _baseScale.x);
        float next = Mathf.Lerp(current, _target, Time.unscaledDeltaTime * speed);
        transform.localScale = _baseScale * next;
    }

    public void OnPointerEnter(PointerEventData eventData) => _target = hoverScale;
    public void OnPointerExit(PointerEventData eventData) => _target = 1f;
}

