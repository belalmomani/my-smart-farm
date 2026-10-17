using UnityEngine;
using UnityEngine.EventSystems;

namespace MySmartFarm.Game
{
    /// <summary>Orbit camera: 1 finger pan, 2 fingers pinch-zoom + twist-rotate. Mouse fallback for the editor. Uses the legacy Input class.</summary>
    public class CameraRig : MonoBehaviour
    {
        Vector3 target = Vector3.zero; float yaw = 20, pitch = 50, dist = 28;
        bool ignoreDrag;

        void LateUpdate()
        {
            HandleInput();
            var rot = Quaternion.Euler(pitch, yaw, 0);
            transform.position = target + rot * new Vector3(0, 0, -dist); transform.rotation = rot;
        }

        bool OverUi(int id) => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(id);

        void Pan(Vector2 d)
        {
            var yawRot = Quaternion.Euler(0, yaw, 0); float k = dist * 0.002f;
            target -= (yawRot * Vector3.right * d.x + yawRot * Vector3.forward * d.y) * k;
            target.x = Mathf.Clamp(target.x, -14, 14); target.z = Mathf.Clamp(target.z, -14, 14);
        }

        void HandleInput()
        {
            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) ignoreDrag = OverUi(t.fingerId);
                if (!ignoreDrag && t.phase == TouchPhase.Moved) Pan(t.deltaPosition);
            }
            else if (Input.touchCount >= 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                if (a.phase == TouchPhase.Began && OverUi(a.fingerId)) ignoreDrag = true;
                if (b.phase == TouchPhase.Began && OverUi(b.fingerId)) ignoreDrag = true;
                if (ignoreDrag) return;
                var pa = a.position - a.deltaPosition; var pb = b.position - b.deltaPosition;
                float prev = (pa - pb).magnitude, cur = (a.position - b.position).magnitude;
                if (cur > 1 && prev > 1) dist = Mathf.Clamp(dist * prev / cur, 8, 45);
                yaw += Vector2.SignedAngle(pa - pb, a.position - b.position) * -1f;
            }
            else
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) ignoreDrag = OverUi(-1);
                if (!ignoreDrag)
                {
                    if (Input.GetMouseButton(0)) Pan(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 20f);
                    if (Input.GetMouseButton(1)) { yaw += Input.GetAxis("Mouse X") * 3f; pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 3f, 20, 80); }
                    float sc = Input.mouseScrollDelta.y; if (Mathf.Abs(sc) > 0.01f) dist = Mathf.Clamp(dist - sc * 2f, 8, 45);
                }
            }
        }
    }
}
