using System;
using System.Collections;
using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>MC FeedDriver port: input in Update, render at WaitForEndOfFrame.</summary>
    internal sealed class FpvFeedDriver : MonoBehaviour
    {
        private Coroutine? _loop;

        private void Update()
        {
            try { FpvFeedCamera.PollInputEarly(); }
            catch { /* never kill driver */ }
        }

        private void OnEnable()
        {
            if (_loop == null)
                _loop = StartCoroutine(RenderLoop());
        }

        private void OnDisable()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }
        }

        private static IEnumerator RenderLoop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            var idleWait = new WaitForSeconds(0.2f);
            while (true)
            {
                try { FpvFeedCamera.TickEndOfFrame(); }
                catch (Exception ex)
                {
                    FpvPlugin.ModLogger?.LogWarning("FPV feed tick: " + ex.Message);
                }

                if (FpvFeedCamera.UseIdleDriverWait)
                    yield return idleWait;
                else
                    yield return endOfFrame;
            }
        }
    }

    internal static class FpvFeedDriverHost
    {
        private static FpvFeedDriver? _driver;

        internal static void Ensure()
        {
            if (_driver != null)
                return;

            var go = new GameObject("FPVMod.FeedDriver");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _driver = go.AddComponent<FpvFeedDriver>();
        }

        internal static void Shutdown()
        {
            if (_driver == null)
                return;
            UnityEngine.Object.Destroy(_driver.gameObject);
            _driver = null;
        }
    }
}
