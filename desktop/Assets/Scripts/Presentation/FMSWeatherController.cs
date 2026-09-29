using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace Virexa.FMS
{
    [Serializable]
    public class CurrentWeatherData
    {
        public double latitude;
        public double longitude;
        public float temperatureC;
        public float humidityPercent;
        public float precipitationMm;
        public float rainMm;
        public float showersMm;
        public float windSpeedKmh;
        public int weatherCode;
        public bool isRaining;
        public string modelTimeUtc;
        public string fetchedAtUtc;
        public string source;
    }

    [Serializable]
    public class CurrentWeatherResponse
    {
        public string status;
        public CurrentWeatherData data;
    }

    public sealed class FMSWeatherController : MonoBehaviour
    {
        public static FMSWeatherController Instance { get; private set; }
        public CurrentWeatherData Current { get; private set; }
        public bool IsAvailable => Current != null && Time.unscaledTime - receivedAt < 600f &&
            FMSCameraController.Instance != null &&
            Vector3.Distance(FMSCameraController.Instance.pivotPoint, reportedPivot) < 500f;

        private ParticleSystem rain;
        private Vector3 queriedPivot;
        private Vector3 reportedPivot;
        private float lastQueryAt = -1000f;
        private float receivedAt = -1000f;
        private bool querying;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (rain != null) Destroy(rain.gameObject);
        }

        private void Update()
        {
            Vector3 pivot = FMSCameraController.Instance != null ? FMSCameraController.Instance.pivotPoint : Vector3.zero;
            UTMCoordinate utm = GeoCoordinateConverter.UnityToUTM(pivot);
            bool inArea = GeoCoordinateConverter.IsInsideMappedTerrain(utm.Easting, utm.Northing);
            if (!inArea)
            {
                Current = null;
                SetRain(false, pivot);
                return;
            }

            if (!querying && (Time.unscaledTime - lastQueryAt > 120f ||
                (Time.unscaledTime - lastQueryAt > 15f && Vector3.Distance(pivot, queriedPivot) > 450f)))
                StartCoroutine(FetchWeather(pivot, utm));

            SetRain(IsAvailable && Current.isRaining, pivot);
        }

        private IEnumerator FetchWeather(Vector3 pivot, UTMCoordinate utm)
        {
            querying = true;
            queriedPivot = pivot;
            lastQueryAt = Time.unscaledTime;
            GeoCoordinateConverter.UTMToLatLon(utm.Easting, utm.Northing, out double lat, out double lon);
            string api = FMSDashboardUI.Instance != null ? FMSDashboardUI.Instance.apiBaseUrl.TrimEnd('/') : "";
            if (string.IsNullOrEmpty(api))
            {
                querying = false;
                yield break;
            }
            string url = api + "/api/v1/weather/current?latitude=" + lat.ToString("F6", CultureInfo.InvariantCulture) +
                "&longitude=" + lon.ToString("F6", CultureInfo.InvariantCulture);
            using (var request = UnityWebRequest.Get(url))
            {
                FMSApiSession.Authorize(request);
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<CurrentWeatherResponse>(request.downloadHandler.text);
                        Current = response != null && response.status == "success" ? response.data : null;
                        if (Current != null)
                        {
                            receivedAt = Time.unscaledTime;
                            reportedPivot = pivot;
                        }
                    }
                    catch { Current = null; }
                }
                else Current = null;
            }
            querying = false;
        }

        private void SetRain(bool active, Vector3 pivot)
        {
            if (!active)
            {
                if (rain != null && rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            if (rain == null)
            {
                var obj = new GameObject("Current Area Rain (Open-Meteo)");
                rain = obj.AddComponent<ParticleSystem>();
                var main = rain.main;
                main.startLifetime = 2.3f;
                main.startSpeed = 0f;
                main.startSize = 0.11f;
                main.startColor = new Color(0.72f, 0.85f, 0.96f, 0.48f);
                main.maxParticles = 1500;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var shape = rain.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(220f, 1f, 220f);
                var velocity = rain.velocityOverLifetime;
                velocity.enabled = true;
                velocity.y = -48f;
                var renderer = rain.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 1.4f;
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) renderer.material = new Material(shader);
                rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            rain.transform.position = pivot + Vector3.up * 105f;
            var emission = rain.emission;
            emission.rateOverTime = Mathf.Clamp((Current.rainMm + Current.showersMm) * 160f, 90f, 500f);
            if (!rain.isPlaying) rain.Play();
        }
    }
}
