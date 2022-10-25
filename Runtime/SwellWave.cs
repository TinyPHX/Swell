using System;
using MyBox;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Swell
{
    /**
     * @brief Customizable wave that can be applied to all waters or to only a small area.  
     */
    [HelpURL("https://tinyphx.github.io/Swell/html/class_swell_1_1_swell_wave.html")]
    public class SwellWave : MonoBehaviour
    {
        public enum Type
        {
            ROUNDED = 1, //sin curve
            POINTED = 5, //square root sin curve 
            RIPPLE = 3, //radial sin curve
            BELL = 4, //gaussian curve
            RANDOM = 2, //perlin noise
            CUSTOM = 6
        }
        
        [field: Separator("Basic Settings")]
        [field: SerializeField, UsePropertyName] public bool WaveEnabled { get; set; } = true; //!< Whether the wave is enabled or not. We use this instead of "Component.enabled" because when disabled we interpolate the wave height back to 0, if interpolation is enabled.
        [field: SerializeField, UsePropertyName] public Type WaveType { get; set; } = Type.ROUNDED; //!< The type of wave curve algorithm
        [field: SerializeField, ConditionalField(nameof(WaveType), false, Type.CUSTOM), UsePropertyName] public AnimationCurve CustomWave { get; set; } = new AnimationCurve(
            new Keyframe(0, 0, 0, 0, 0, 0),
            new Keyframe(.25f, -1, 0, 0, .5f, .5f),
            new Keyframe(.5f, 0, 0, 0, 0, 0),
            new Keyframe(.75f, 1, 0, 0, .5f, .5f),
            new Keyframe(1, 0, 0, 0, 0, 0)
        ); //!< If the WaveType is set to CUSTOM, this this AnimationCurve is used to set the curvature of the wave. 
        [field: SerializeField, UsePropertyName] public float WaveHeight { get; set; } = 1; //!< The height of the wave from origin to to peak. The lenght from valley to peak is double this for some wave types
        [SerializeField] private Vector2 waveScale = Vector2.one; 
        public Vector2 WaveScale { get => waveScale; set => waveScale = value; } //!< The phase of the wave. A larger number means closer peaks.
        [SerializeField] private Vector2 waveOffset = Vector2.zero;
        public Vector2 WaveOffset { get => waveOffset; set => waveOffset = value; } //!< An offset to the time component of the wave.
        [SerializeField] private Vector2 waveSpeed = Vector2.one * .1f;
        public Vector2 WaveSpeed { get => waveSpeed; set => waveSpeed = value; } //!< A multiplier to the rate of change in the offset of the wave.  
        [field: SerializeField, UsePropertyName, Range(0, 360)] public float WaveRotation { get; set; } = 0; //!< Degrees offset of the rotation of the wave. This can be expensive! 
        
        [field: Separator("Spread")] 
        [field: OverrideLabel(""), SerializeField, UsePropertyName] private bool spread = false;
        public bool Spread { get => spread; set => spread = value; } //!< When true a spread multiplier is calculated for each point adjusting the range of the wave.  
        [field: SerializeField, ConditionalField(nameof(spread)), UsePropertyName] public float SpreadRadius { get; set; } = 10; //!< The area of affect of the wave.
        [field: SerializeField, ConditionalField(nameof(spread)), UsePropertyName] public AnimationCurve SpreadCurve { get; set; } = new AnimationCurve(
            new Keyframe(0, 1, 0, 0, .5f, .5f),
            new Keyframe(1, 0, 0, 0, .5f, .5f)
        ); //!< The rate at which the spread is adjusted.

        [Separator("Interpolate")]
        [OverrideLabel(""), SerializeField] private bool interpolate = true;
        public bool Interpolate { get => interpolate; set => interpolate = value; } //!< When true the waves height is interpolated on Start() and when WaveEnabled is changed. 
        [field: SerializeField, ConditionalField(nameof(interpolate)), UsePropertyName] public float InterpolationTime { get; set; } = 10; //!< The time in seconds it takes to interpolate to full height.
        [field: SerializeField, ConditionalField(nameof(interpolate)), UsePropertyName] public AnimationCurve InterpolationCurve { get; set; } = new AnimationCurve(
            new Keyframe(0, 0, 0, 0, .5f, .5f),
            new Keyframe(1, 1, 0, 0, .5f, .5f)
        ); //!< The rate at which interpolation is applied.

        [Separator("Fluctuate")]
        [OverrideLabel(""), SerializeField] private bool fluctuate = false;
        public bool Fluctuate { get => fluctuate; set => fluctuate = value; } //!< When true the waves's height fluctuates between WaveHeight and -WaveHeight.
        [field: SerializeField, ConditionalField(nameof(fluctuate)), UsePropertyName] public float FluctuatePeriodTime { get; set; } = 10; //!< The time in seconds it takes do one fluctuate loop.
        [field: SerializeField, ConditionalField(nameof(fluctuate)), Range(0, 1), UsePropertyName] public float FluctuateOffset { get; set; } = 0; //!< An offset to the time component of fluctuate.
        [field: SerializeField, ConditionalField(nameof(fluctuate)), UsePropertyName] public AnimationCurve FluctuateCurve { get; set; } = new AnimationCurve(
            new Keyframe(0, -1, 0, 0, .5f, .5f),
            new Keyframe(.5f, 1, 0, 0, .5f, .5f),
            new Keyframe(1, -1, 0, 0, .5f, .5f)
        ); //!< The rate at which the height fluctuates.

        private float adjustedWaveHeight;
        private bool previousWaveEnabled;
        private bool previousInterpolate;
        private float interpolateStartTime;
        private Vector3 position; //optimize access of property;
        private bool activeAndEnabled;  //optimize access of property;
        private Vector2 adjustedOffset;
        private Vector2 adjustedPosition;
        private float time;
        private float startTime;
        private bool initialized = false;

        private void Reset()
        {
            UpdateActive();
        }

        private void Start()
        {
            this.Register();

            if (interpolate && WaveEnabled)
            {
                previousInterpolate = interpolate;
                previousWaveEnabled = WaveEnabled;
                interpolateStartTime = startTime;
            }
        }

        private void Initialize()
        {
            if (!initialized)
            {
                initialized = true;
                Start();
            }
        }

        public void Update()
        {
            time = WaveTime;
            startTime = WaveStartTime;
            
            Initialize();
            
            UpdateActive();
            UpdateAdjustedWaveHeight();
            position = transform.position;
        }

        private void OnDisable()
        {
            UpdateActive();
        }

        private void OnEnable()
        {
            UpdateActive();
        }

        private void UpdateActive()
        {
            activeAndEnabled = isActiveAndEnabled;
        }

        private void OnDestroy()
        {
            this.UnRegister();
        }

        private void UpdateAdjustedWaveHeight()
        {
            float tempWaveHeight = WaveEnabled || interpolate ? WaveHeight : 0;
            adjustedWaveHeight = tempWaveHeight * GetInterpolateRatio() * GetFluctuateRatio();
        }

        private float GetFluctuateRatio()
        {
            float fluctuateRatio;

            if (fluctuate)
            {
                fluctuateRatio = FluctuateCurve.Evaluate((time / FluctuatePeriodTime + FluctuateOffset) %
                                                         FluctuateCurve.keys[FluctuateCurve.length - 1].time);
            }
            else
            {
                fluctuateRatio = 1;
            }

            return fluctuateRatio;
        }

        private float GetInterpolateRatio()
        {
            float interpolateRatio = 1;

            if (interpolate != previousInterpolate)
            {
                previousInterpolate = interpolate;
                if (interpolate)
                {
                    interpolateStartTime = startTime;
                    if (WaveHeight != 0)
                    {
                        float currentHeightRatio = adjustedWaveHeight / WaveHeight;
                        currentHeightRatio = WaveEnabled ? currentHeightRatio : 1 - currentHeightRatio;
                        interpolateStartTime = startTime - InterpolationTime * currentHeightRatio;
                    }
                }
            }

            if (WaveEnabled != previousWaveEnabled)
            {
                previousWaveEnabled = WaveEnabled;

                if (interpolate)
                {
                    float timeSinceStart = time - interpolateStartTime;
                    if (timeSinceStart < InterpolationTime)
                    {
                        interpolateStartTime = time - (InterpolationTime - timeSinceStart);
                    }
                    else
                    {
                        interpolateStartTime = startTime;
                    }
                }
            }

            if (interpolate)
            {
                float timeSinceStart = time - interpolateStartTime;
                float curveRatio = InterpolationTime == 0 ? 1 : timeSinceStart / InterpolationTime;
                curveRatio = WaveEnabled ? curveRatio : 1 - curveRatio;
                interpolateRatio = InterpolationCurve.Evaluate(curveRatio);
            }

            return interpolateRatio;
        }

        private float GetSpread(float spreadPositionX, float spreadPositionY)
        {
            spreadPositionX -= 1;
            spreadPositionY -= 1;
            
            float xSpradRatio = (spreadPositionX * spreadPositionX) / (SpreadRadius * SpreadRadius);
            float ySpradRatio = (spreadPositionY * spreadPositionY) / (SpreadRadius * SpreadRadius);
            xSpradRatio = xSpradRatio > 1 ? 1 : xSpradRatio;
            ySpradRatio = ySpradRatio > 1 ? 1 : ySpradRatio;
            
            float curveTime = 1 - (1 - xSpradRatio) * (1 - ySpradRatio);
            return SpreadCurve.Evaluate(curveTime);
        }
          
        /**
         * @brief Returns the height of this wave at the given position. 
         * 
         * Optionally allows ignoring of interpolation.
         * 
         * @param x The x position to be used in our height check
         * @param y The x position to be used in our height check
         * @param ignoreInterpolation Optional param that when set to true will return the true height ignoring time passed and interpolation. 
         * 
         * TODO: Possible optimization: If for each wave we figured out the phase we can calculate the height only across the phase once then use mod to lookup the height on repeated patterns. Still would have to calculate spread.
         */
        public float GetHeight(float x, float y, bool ignoreInterpolation = false)
        {
            if (!activeAndEnabled)
            {
                return 0;
            }

            // https://www.wolframalpha.com/input/?i=2%5E%28-+%28x%5E2+%2F+%282*2%5E2%29%29+-+%28y%5E2+%2F+%282*2%5E2%29%29%29+++++++x%3D-5+to+5+y%3D-5+to5
            // https://www.wolframalpha.com/input/?i=Gaussian+Distribution
            float spreadMultiplier = 1;

            if (spread)
            {
                float spreadPositionX = x - position.x;
                float spreadPositionY = y - position.z;
                spreadMultiplier = GetSpread(spreadPositionX, spreadPositionY);
            }
            
            if (WaveRotation != 0)
            {
                // TODO: Should consider rotating grids in bulk earlier with unit quaternion
                // https://stackoverflow.com/questions/62974296/rotating-multiple-points-around-axis-using-quaternion
                Vector3 rotatedPosition = Quaternion.AngleAxis(-WaveRotation, Vector3.up) *
                                          new Vector3(position.x - x, 0, position.z - y);
                x = rotatedPosition.x + position.x;
                y = rotatedPosition.z + position.z;
            }

            float height = 0;
            if (spreadMultiplier > 0)
            {
                if (WaveType == Type.ROUNDED)
                {
                    //https://www.wolframalpha.com/input/?i=sin%28x%29%2C+x%3D-5+to+5+y%3D-5+to+5

                    adjustedOffset = new Vector2(
                        waveOffset.x + waveSpeed.x * time,
                        waveOffset.y + waveSpeed.y * time
                    );

                    height = Mathf.Sin((x * waveScale.x / 10 + adjustedOffset.x) * Mathf.PI + 1) *
                             Mathf.Sin((y * waveScale.y / 10 + adjustedOffset.y) * Mathf.PI + 1);
                }
                else if (WaveType == Type.POINTED)
                {
                    adjustedOffset = new Vector2(
                        waveOffset.x + waveSpeed.x * time,
                        waveOffset.y + waveSpeed.y * time
                    );

                    //https://www.wolframalpha.com/input?i=plot+y+%3D+-%28sqrt%28sin%28x%29+%2B+1%29+%2B+sqrt%28sin%28z%29+%2B+1%29+*+sqrt%282%29+-+1%29
                    //-(sqrt(sin(x) + 1) + sqrt(sin(z) + 1) * sqrt(2) - 1)

                    float maxHeight = Mathf.Sqrt(Mathf.PI + Mathf.PI) / 2 + Mathf.Sqrt(Mathf.PI + Mathf.PI) / 2;

                    height = -1 * (
                        (
                            Mathf.Sqrt(Mathf.Sin(x * waveScale.x * 2 * Mathf.PI / 10 + adjustedOffset.x) * Mathf.PI +
                                       Mathf.PI) +
                            Mathf.Sqrt(Mathf.Sin(y * waveScale.y * 2 * Mathf.PI / 10 + adjustedOffset.y) * Mathf.PI +
                                       Mathf.PI)
                        ) / maxHeight - 1);
                }
                else if (WaveType == Type.RIPPLE)
                {
                    //https://www.wolframalpha.com/input/?i=sin%283+*+%28+sqrt%28x*x+%2B+y*y%29+%2F+10+%29+*+pi*2+%2B+1%29%2C+x%3D-5+to+5+y%3D-5+to5

                    //waveScale.y, waveSpeed.y, and waveOffset.y not used.
                    waveScale.y = waveScale.x;
                    waveSpeed.y = waveSpeed.x;
                    waveOffset.y = waveOffset.x;

                    adjustedOffset = new Vector2(
                        waveOffset.x + waveSpeed.x * time,
                        waveOffset.y + waveSpeed.y * time
                    );
                    Vector2 delta = new Vector2(
                        (x - position.x) * waveScale.x,
                        (y - position.z) * waveScale.x
                    );

                    //Formula: Math.abs((Math.sin(WAVE_COUNT * (Math.sqrt((x) * (x) + (y) * (y)) / WIDTH) * RAD + phaseShift) + 1)
                    height = Mathf.Sin(Mathf.Sqrt(delta.x * delta.x + delta.y * delta.y) / 10 * Mathf.PI * 2 +
                                       adjustedOffset.x);
                }
                else if (WaveType == Type.BELL)
                {
                    waveSpeed = Vector2.zero;

                    Vector2 delta = new Vector2(x - position.x, y - position.z);
                    float slope = 1 + (waveOffset.x > 0 ? 1 / waveOffset.x : 0);
                    float distance = 1 + waveOffset.y / 10;
                    if (waveScale.x > 0 && waveScale.y > 0)
                    {
                        delta.x *= 10 / waveScale.x;
                        delta.y *= 10 / waveScale.y;
                    }

                    height = Mathf.Pow(slope,
                        -delta.x * delta.x / (2 * distance * distance)
                        - delta.y * delta.y / (2 * distance * distance));
                }
                else if (WaveType == Type.RANDOM)
                {
                    Vector2 offset = time * waveSpeed + waveOffset;
                    //Noise reflects at 0 so we offset as much as possible. 
                    float reflectionOffset = 50000;
                    Vector3 delta = new Vector2(
                        x * .1f * waveScale.x + offset.x + reflectionOffset,
                        y * .1f * waveScale.y + offset.y + reflectionOffset
                    );
                    height = 2 * Mathf.PerlinNoise(
                        delta.x,
                        delta.y
                    ) - 1;
                }
                else if (WaveType == Type.CUSTOM)
                {
                    adjustedOffset.x = waveOffset.x + waveSpeed.x * time;
                    adjustedOffset.y = waveOffset.y + waveSpeed.y * time;
                    adjustedPosition.x = x * .05f * waveScale.x + adjustedOffset.x;
                    adjustedPosition.y = y * .05f * waveScale.y + adjustedOffset.y;

                    //Wrap between 0 and 1 to be used for AnimationCurve.Evaluate. 
                    adjustedPosition.x %= 1;
                    adjustedPosition.y %= 1;
                    if (adjustedPosition.x < 0)
                    {
                        adjustedPosition.x = 1 + adjustedPosition.x;
                    }

                    if (adjustedPosition.y < 0)
                    {
                        adjustedPosition.y = 1 + adjustedPosition.y;
                    }

                    height =
                        (CustomWave.Evaluate(adjustedPosition.x) +
                         CustomWave.Evaluate(adjustedPosition.y)) / 2;
                }
            }

            height *= ignoreInterpolation ? 1 : adjustedWaveHeight;
            height *= spreadMultiplier;

            return height;
        }

        private float WaveTime
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    return 10000;
                }
#endif
                return Time.time;
            }
        }

        private float WaveStartTime
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    return 0;
                }
#endif
                return Time.time;
            }
        }
    }
}