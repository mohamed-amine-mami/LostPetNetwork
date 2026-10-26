using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LostPetNetwork.Tests.PlayMode
{
    public class Preview3DTests
    {
        GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
        }

        static int CountOpaque(Texture2D tex)
        {
            int n = 0;
            foreach (var p in tex.GetPixels32())
                if (p.a > 200) n++;
            return n;
        }

        LostPetNetwork.UI.Preview3D CreatePreview()
        {
            _host = new GameObject("PreviewHost", typeof(RectTransform), typeof(RawImage));
            return _host.AddComponent<LostPetNetwork.UI.Preview3D>();
        }

        [UnityTest]
        public IEnumerator ShowsAnObjectOnATransparentBackground()
        {
            var preview = CreatePreview();
            yield return null;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.SetContent(cube);
            yield return null;
            yield return null;
            yield return null;

            var shot = preview.Capture();
            int opaque = CountOpaque(shot);
            int total = shot.width * shot.height;
            Object.Destroy(shot);

            Assert.Greater(opaque, total / 50, "the object should cover part of the picture");
            Assert.Less(opaque, total * 9 / 10, "the background should stay transparent");
        }

        [UnityTest]
        public IEnumerator DragRotatesAndZoomIsClamped()
        {
            var preview = CreatePreview();
            yield return null;
            preview.SetContent(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            yield return null;

            preview.SetYaw(90f);
            Assert.AreEqual(90f, preview.Yaw, 0.001f);
            Assert.AreEqual(90f, preview.Pivot.localEulerAngles.y, 0.01f);

            preview.SetZoom(100f);
            Assert.AreEqual(2.5f, preview.Zoom, 0.001f);
            preview.SetZoom(0f);
            Assert.AreEqual(0.5f, preview.Zoom, 0.001f);
            preview.ResetView();
            Assert.AreEqual(1f, preview.Zoom, 0.001f);
        }

        [UnityTest]
        public IEnumerator TwoPreviewsDoNotShareAStage()
        {
            var first = CreatePreview();
            yield return null;
            var secondHost = new GameObject("PreviewHost2", typeof(RectTransform), typeof(RawImage));
            var second = secondHost.AddComponent<LostPetNetwork.UI.Preview3D>();
            yield return null;

            Assert.AreNotEqual(first.PreviewCamera.transform.position, second.PreviewCamera.transform.position);
            Assert.AreNotSame(first.PreviewCamera.targetTexture, second.PreviewCamera.targetTexture);
            Object.Destroy(secondHost);
        }
    }
}
