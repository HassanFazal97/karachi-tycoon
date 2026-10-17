using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // The street camera. Input arrives through a full-screen UI element behind the HUD, so the
    // HUD always gets first pick of a touch and the same code handles mouse, trackpad and fingers:
    // drag to pan (with a little inertia), pinch or scroll to zoom, tap to select.
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public float ViewX, ViewWidth = 30, YOffset;
        public const float MinX = -20, MaxX = 20;
        // Portrait phones frame the cart and queue tall, so they see a narrower slice of the road.
        public float MinWidth = 12, MaxWidth = 50;

        // Screen position (pixels, origin bottom-left) of a tap or click that did not drag.
        public event Action<Vector2> Tapped;

        VisualElement surface;
        readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        Vector2 downPos; float downTime; bool dragged; float velocity; float lastPinch;

        public void Init(Camera cam, bool phone)
        {
            Cam = cam;
            if (phone) { ViewWidth = 13; MinWidth = 7; MaxWidth = 26; }
            else ViewWidth = 30;
            Apply();
        }

        public void Attach(VisualElement world)
        {
            surface = world;
            world.RegisterCallback<PointerDownEvent>(Down);
            world.RegisterCallback<PointerMoveEvent>(Move);
            world.RegisterCallback<PointerUpEvent>(Up);
            world.RegisterCallback<PointerCancelEvent>(e => { pointers.Remove(e.pointerId); world.ReleasePointer(e.pointerId); });
            world.RegisterCallback<WheelEvent>(e => Zoom(e.delta.y > 0 ? 1.1f : 0.9f));
        }

        void Down(PointerDownEvent e)
        {
            pointers[e.pointerId] = e.position;
            surface.CapturePointer(e.pointerId);
            velocity = 0;
            if (pointers.Count == 1) { downPos = e.position; downTime = Time.unscaledTime; dragged = false; }
            else lastPinch = PinchDistance();
        }

        void Move(PointerMoveEvent e)
        {
            if (!pointers.TryGetValue(e.pointerId, out var prev)) return;
            pointers[e.pointerId] = e.position;
            float width = Mathf.Max(1, surface.layout.width);
            if (pointers.Count == 1)
            {
                if ((((Vector2)e.position) - downPos).sqrMagnitude > 64) dragged = true;
                if (!dragged) return;
                float dx = (e.position.x - prev.x) / width * ViewWidth * 1.1f;
                ViewX = Mathf.Clamp(ViewX - dx, MinX, MaxX);
                velocity = Mathf.Lerp(velocity, -dx / Mathf.Max(Time.unscaledDeltaTime, 0.008f), 0.4f);
            }
            else if (pointers.Count == 2)
            {
                dragged = true;
                float d = PinchDistance();
                if (lastPinch > 10 && d > 10) Zoom(lastPinch / d);
                lastPinch = d;
            }
        }

        void Up(PointerUpEvent e)
        {
            pointers.Remove(e.pointerId);
            surface.ReleasePointer(e.pointerId);
            if (pointers.Count == 0 && !dragged && Time.unscaledTime - downTime < 0.4f)
            {
                // Panel coordinates are scaled and top-left based; turn them back into screen pixels.
                float k = Screen.width / Mathf.Max(1, surface.layout.width);
                Tapped?.Invoke(new Vector2(e.position.x * k, Screen.height - e.position.y * k));
            }
            if (pointers.Count == 1) foreach (var kv in pointers) { downPos = kv.Value; }
        }

        float PinchDistance()
        {
            var it = pointers.Values.GetEnumerator();
            it.MoveNext(); var a = it.Current; it.MoveNext(); var b = it.Current;
            return Vector2.Distance(a, b);
        }

        // The district view: pull right back to see the whole food street, and return to where we were.
        float savedX, savedWidth, savedMax, savedY;
        public bool District { get; private set; }
        public void SetDistrictView(bool on, bool phone)
        {
            if (on == District) return;
            District = on;
            if (on) { savedX = ViewX; savedWidth = ViewWidth; savedY = YOffset; savedMax = MaxWidth; MaxWidth = 80; ViewX = 0; ViewWidth = phone ? 30 : 72; YOffset = phone ? 6 : 2; }
            else { ViewX = savedX; ViewWidth = savedWidth; YOffset = savedY; MaxWidth = savedMax; }
        }

        // Frame the player's whole frontage for their tier: from the thela up to the restaurant's roof.
        static readonly float[] DeskWidth = { 30, 30, 30, 22, 26, 30 }, PhoneWidth = { 13, 13, 14, 14, 16, 18 }, Lift = { 0, 0, 0, 0.6f, 2.2f, 3.6f };
        public void FrameTier(int tier, bool phone)
        {
            if (District || tier < 0) return;
            int i = Mathf.Clamp(tier, 0, Lift.Length - 1);
            ViewWidth = phone ? PhoneWidth[i] : DeskWidth[i];
            YOffset = phone ? Lift[i] * 1.6f : Lift[i];
            ViewX = tier >= 3 ? 3.2f : 1.5f;
            velocity = 0;
        }

        public void Pan(float dx) { ViewX = Mathf.Clamp(ViewX + dx, MinX, MaxX); velocity = 0; }
        public void Zoom(float factor) => ViewWidth = Mathf.Clamp(ViewWidth * factor, MinWidth, MaxWidth);

        void LateUpdate()
        {
            if (pointers.Count == 0 && Mathf.Abs(velocity) > 0.01f)
            {
                ViewX = Mathf.Clamp(ViewX + velocity * Time.unscaledDeltaTime, MinX, MaxX);
                velocity *= Mathf.Exp(-5 * Time.unscaledDeltaTime);
            }
            Apply();
        }

        void Apply()
        {
            if (Cam == null) return;
            // pixelWidth/Height follow a target texture too (headless screenshots).
            float aspect = (float)Cam.pixelWidth / Mathf.Max(1, Cam.pixelHeight);
            Cam.orthographicSize = ViewWidth / aspect / 2;
            var target = W(ViewX, 3.2f + ViewWidth * 0.06f + YOffset, -3.5f);
            var dir = new Vector3(0.22f, 0.6f, -0.77f).normalized;
            // Orthographic, so distance only matters for shadows: close enough to stay inside the shadow distance.
            Cam.transform.position = target + dir * 45;
            Cam.transform.LookAt(target);
        }
    }
}
