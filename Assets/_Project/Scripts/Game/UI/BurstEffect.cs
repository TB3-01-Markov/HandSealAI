using UnityEngine;
using UnityEngine.UI;

namespace HandSeals.Game
{
    /// <summary>
    /// Celebration effects drawn with plain UI images, so they need no particle assets:
    /// a burst of sparks that fly out, fall and fade, and a full-screen colour flash.
    /// </summary>
    internal sealed class BurstEffect : MonoBehaviour
    {
        private const int MaxSparks = 40;
        private const float Gravity = 1400f;
        private const float Drag = 2.5f;
        private const float FlashSeconds = 0.45f;

        private readonly System.Random _random = new System.Random();

        private RectTransform[] _sparks;
        private Image[] _images;
        private Vector2[] _positions;
        private Vector2[] _velocities;
        private float[] _ages;
        private float[] _lives;
        private Color[] _colors;
        private Image _flash;
        private Color _flashColor;
        private float _flashAge = FlashSeconds;

        /// <summary>Creates the flash and the spark pool under <paramref name="layer"/>, a full-screen rectangle.</summary>
        public void Build(RectTransform layer)
        {
            _flash = UiFactory.AddImage(UiFactory.CreateRect("Flash", layer, Vector2.zero, Vector2.one), Color.clear);
            _flash.enabled = false;

            _sparks = new RectTransform[MaxSparks];
            _images = new Image[MaxSparks];
            _positions = new Vector2[MaxSparks];
            _velocities = new Vector2[MaxSparks];
            _ages = new float[MaxSparks];
            _lives = new float[MaxSparks];
            _colors = new Color[MaxSparks];

            for (int i = 0; i < MaxSparks; i++)
            {
                _sparks[i] = UiFactory.CreateCentered("Spark", layer, new Vector2(24f, 24f));
                _images[i] = UiFactory.AddImage(_sparks[i], Color.white);
                _sparks[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Sends <paramref name="count"/> sparks outwards from a point given relative to the screen centre.</summary>
        public void Burst(Vector2 origin, Color color, int count)
        {
            if (_sparks == null)
            {
                return;
            }

            int started = 0;
            for (int i = 0; i < MaxSparks && started < count; i++)
            {
                if (_sparks[i].gameObject.activeSelf)
                {
                    continue;
                }

                float angle = (float)(_random.NextDouble() * 2.0 * System.Math.PI);
                float speed = 700f + (float)_random.NextDouble() * 900f;

                _positions[i] = origin;
                _velocities[i] = new Vector2((float)System.Math.Cos(angle) * speed, (float)System.Math.Sin(angle) * speed + 350f);
                _ages[i] = 0f;
                _lives[i] = 0.7f + (float)_random.NextDouble() * 0.5f;

                // Mix in some white so the burst sparkles instead of being one flat colour.
                float whiteness = (float)_random.NextDouble() * 0.5f;
                _colors[i] = new Color(
                    color.r + (1f - color.r) * whiteness,
                    color.g + (1f - color.g) * whiteness,
                    color.b + (1f - color.b) * whiteness,
                    1f);

                _sparks[i].anchoredPosition = origin;
                _sparks[i].localScale = Vector3.one;
                _sparks[i].localEulerAngles = new Vector3(0f, 0f, angle * 57.29578f);
                _images[i].color = _colors[i];
                _sparks[i].gameObject.SetActive(true);
                started++;
            }
        }

        /// <summary>Tints the whole screen for a moment; <paramref name="color"/> alpha sets the strength.</summary>
        public void Flash(Color color)
        {
            if (_flash == null)
            {
                return;
            }

            _flashColor = color;
            _flashAge = 0f;
            _flash.color = color;
            _flash.enabled = true;
        }

        private void Update()
        {
            if (_sparks == null)
            {
                return;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            if (_flash.enabled)
            {
                _flashAge += dt;
                if (_flashAge >= FlashSeconds)
                {
                    _flash.enabled = false;
                }
                else
                {
                    float alpha = _flashColor.a * (1f - _flashAge / FlashSeconds);
                    _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, alpha);
                }
            }

            float damping = Mathf.Max(0f, 1f - Drag * dt);

            for (int i = 0; i < MaxSparks; i++)
            {
                if (!_sparks[i].gameObject.activeSelf)
                {
                    continue;
                }

                _ages[i] += dt;
                float life = _ages[i] / _lives[i];
                if (life >= 1f)
                {
                    _sparks[i].gameObject.SetActive(false);
                    continue;
                }

                Vector2 velocity = _velocities[i];
                velocity = new Vector2(velocity.x * damping, velocity.y * damping - Gravity * dt);
                _velocities[i] = velocity;
                _positions[i] = new Vector2(_positions[i].x + velocity.x * dt, _positions[i].y + velocity.y * dt);

                float scale = 1f - 0.6f * life;
                _sparks[i].anchoredPosition = _positions[i];
                _sparks[i].localScale = new Vector3(scale, scale, 1f);

                Color color = _colors[i];
                _images[i].color = new Color(color.r, color.g, color.b, 1f - life * life);
            }
        }
    }
}
