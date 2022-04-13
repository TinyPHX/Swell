using System;
using TP.ExtensionMethods;
using UnityEngine;
using UnityEngine.Profiling;

namespace Swell
{
    /**
     * @brief Customizable wave that can be applied to all waters or to only a small area.  
     */
    public class SwellWave : MonoBehaviour
    {
        public enum Type
        {
            rounded = 1, //sin curve
            pointed = 5, //square root sin curve 
            ripple = 3, //radial sin curve
            bell = 4, //gaussian curve
            random = 2, //perlin noise
            custom = 6
        }

        private const string H1 = " ";

        [Header(H1 + "Basic Settings" + H1)] [SerializeField]
        private bool waveEnabled = true;

        [SerializeField] private Type waveType = Type.rounded;

        [SerializeField] private AnimationCurve customWave = new AnimationCurve(new Keyframe[]
        {
            new Keyframe(0, 0, 0, 0, 0, 0),
            new Keyframe(.25f, -1, 0, 0, .5f, .5f),
            new Keyframe(.5f, 0, 0, 0, 0, 0),
            new Keyframe(.75f, 1, 0, 0, .5f, .5f),
            new Keyframe(1, 0, 0, 0, 0, 0),
        });

        [SerializeField] private float waveHeight = 1;
        [SerializeField] private Vector2 waveScale = Vector2.one;
        [SerializeField] private Vector2 waveOffset = Vector2.one;
        [SerializeField] private Vector2 waveSpeed = Vector2.zero;
        [SerializeField, Range(0, 360)] private float waveRotation = 0;

        [Header(H1 + "Spread" + H1)] [SerializeField]
        private float spread = 0;

        [SerializeField] private AnimationCurve spreadCurve = new AnimationCurve(new Keyframe[]
        {
            new Keyframe(0, 1, 0, 0, .5f, .5f),
            new Keyframe(1, 0, 0, 0, .5f, .5f),
        });

        [Header(H1 + "Interpolate" + H1)] [SerializeField]
        private bool interpolate = true;

        [SerializeField] private float interpolationTime = 10;

        [SerializeField] private AnimationCurve interpolationCurve = new AnimationCurve(new Keyframe[]
        {
            new Keyframe(0, 0, 0, 0, .5f, .5f),
            new Keyframe(1, 1, 0, 0, .5f, .5f),
        });

        [Header(H1 + "Fluctuate" + H1)] [SerializeField]
        private bool fluctuate = false;

        [SerializeField] private float fluctuatePeriodTime = 10;
        [SerializeField, Range(0, 1)] private float fluctuateOffset = 0;

        [SerializeField] private AnimationCurve fluctuateCurve = new AnimationCurve(new Keyframe[]
        {
            new Keyframe(0, -1, 0, 0, .5f, .5f),
            new Keyframe(.5f, 1, 0, 0, .5f, .5f),
            new Keyframe(1, -1, 0, 0, .5f, .5f)
        });

        private float adjustedWaveHeight;
        private bool previousWaveEnabled;
        private bool previousInterpolate;
        private float interpolateStartTime;
        private Vector3 position; //optimize access of property;
        private bool activeAndEnabled;  //optimize access of property;
        private Vector2 adjustedOffset;
        private Vector2 adjustedPosition;

        void Start()
        {
            SwellManager.Register(this);

            if (interpolate && waveEnabled)
            {
                previousInterpolate = interpolate;
                previousWaveEnabled = waveEnabled;
                interpolateStartTime = Time.time;
            }
        }

        private void Update()
        {
            UpdateAdjustedWaveHeight();
            position = transform.position;
            activeAndEnabled = isActiveAndEnabled;
        }

        private void OnDestroy()
        {
            SwellManager.Unregister(this);
        }

        void UpdateAdjustedWaveHeight()
        {
            float tempWaveHeight = waveEnabled || interpolate ? waveHeight : 0;
            adjustedWaveHeight = tempWaveHeight * GetInterpolateRatio() * GetFluctuateRatio();
        }

        public float GetFluctuateRatio()
        {
            float fluctuateRatio;

            if (fluctuate)
            {
                fluctuateRatio = fluctuateCurve.Evaluate((Time.time / fluctuatePeriodTime + fluctuateOffset) %
                                                         fluctuateCurve.keys[fluctuateCurve.length - 1].time);
            }
            else
            {
                fluctuateRatio = 1;
            }

            return fluctuateRatio;
        }

        public float GetInterpolateRatio()
        {
            float interpolateRatio = 1;

            if (interpolate != previousInterpolate)
            {
                previousInterpolate = interpolate;
                if (interpolate)
                {
                    interpolateStartTime = Time.time;
                    if (waveHeight != 0)
                    {
                        float currentHeightRatio = adjustedWaveHeight / waveHeight;
                        currentHeightRatio = waveEnabled ? currentHeightRatio : 1 - currentHeightRatio;
                        interpolateStartTime = Time.time - interpolationTime * currentHeightRatio;
                    }
                }
            }

            if (waveEnabled != previousWaveEnabled)
            {
                previousWaveEnabled = waveEnabled;

                if (interpolate)
                {
                    float timeSinceStart = Time.time - interpolateStartTime;
                    if (timeSinceStart < interpolationTime)
                    {
                        interpolateStartTime = Time.time - (interpolationTime - timeSinceStart);
                    }
                    else
                    {
                        interpolateStartTime = Time.time;
                    }
                }
            }

            if (interpolate)
            {
                float timeSinceStart = Time.time - interpolateStartTime;
                float curveRatio = interpolationTime == 0 ? 1 : timeSinceStart / interpolationTime;
                curveRatio = waveEnabled ? curveRatio : 1 - curveRatio;
                interpolateRatio = interpolationCurve.Evaluate(curveRatio);
            }

            return interpolateRatio;
        }

        public float[] GetNormal(float x, float y)
        {
            float[] normal = new[] {0f, 0f};
            if (waveType == Type.rounded)
            {
                //TODO calculate tangent from curve instead of using the geometry.
                //1. Create derivative function for each wave type. Example: dx/dy sin(x) = cos(x)
                //   https://www.derivative-calculator.net/
                //2. The derivitive gives you the slope (tangent) at each position
                //3. Use the slope to calculate normal
                //4. Return normal vector.

                //?? Is this easier or faster than sampling more points to get slope? That method would also work for
                //perlin where the above method does not. 
            }
            else if (waveType == Type.ripple)
            {
            }
            else if (waveType == Type.bell)
            {
            }
            else if (waveType == Type.random)
            {
            }
            else if (waveType == Type.custom)
            {
            }

            return normal;
        }

        public float GetSpread(float spreadPositionX, float spreadPositionY)
        {
            float xSpradRatio = (spreadPositionX < 0 ? -spreadPositionX : spreadPositionX) / spread;
            xSpradRatio = xSpradRatio > 1 ? 1 : xSpradRatio;
            float ySpradRatio = (spreadPositionY < 0 ? -spreadPositionY : spreadPositionY) / spread;
            ySpradRatio = ySpradRatio > 1 ? 1 : ySpradRatio;
            float curveTime = 1 - (1 - xSpradRatio) * (1 - ySpradRatio);
            return spreadCurve.Evaluate(curveTime);

            //The above is an optimize version of this code. Including because it's hard to read. Basically just replaced
            //min and abs functions.
            // float xSpradRatio = Mathf.Min(Mathf.Abs(spreadPositionX) / spread, 1);
            // float ySpradRatio = Mathf.Min(Mathf.Abs(spreadPositionY) / spread, 1);
            // float curveTime = 1 - (1 - xSpradRatio) * (1 - ySpradRatio);
            // return spreadCurve.Evaluate(curveTime);
        }

        //Possible optimization: If for each wave we figured out the phase we can calculate the height only across the
        //phase once then use mod to lookup the height on repeated patterns. Still would have to calculate spread.  
        public float GetHeight(float x, float y, bool ignoreInterpolation = false)
        {
            // Profiler.BeginSample("isActiveAndEnabled");
            if (!activeAndEnabled)
            {
                return 0;
            }
            // Profiler.EndSample();

            //  https://www.wolframalpha.com/input/?i=2%5E%28-+%28x%5E2+%2F+%282*2%5E2%29%29+-+%28y%5E2+%2F+%282*2%5E2%29%29%29+++++++x%3D-5+to+5+y%3D-5+to5
            //  https://www.wolframalpha.com/input/?i=Gaussian+Distribution
            float spreadMultiplier = 1;

            
            // Profiler.BeginSample("Spread");

            if (spread > 0)
            {
                float spreadPositionX = x - position.x;
                float spreadPositionY = y - position.z;
                spreadMultiplier = GetSpread(spreadPositionX, spreadPositionY);
            }

            // Profiler.EndSample();

            // Profiler.BeginSample("waveRotation");
            if (waveRotation != 0)
            {
                //  Should consider rotating grids in bulk earlier with unit quaternion
                //  https://stackoverflow.com/questions/62974296/rotating-multiple-points-around-axis-using-quaternion
                Vector3 rotatedPosition = Quaternion.AngleAxis(-waveRotation, Vector3.up) *
                                          new Vector3(position.x - x, 0, position.z - y);
                x = rotatedPosition.x + position.x;
                y = rotatedPosition.z + position.z;
            }
            // Profiler.EndSample();

            float height = 0;
            if (spreadMultiplier > 0)
            {

                // Profiler.BeginSample("Calculate wave height");
                if (waveType == Type.rounded)
                {
                    //https://www.wolframalpha.com/input/?i=sin%28x%29%2C+x%3D-5+to+5+y%3D-5+to+5

                    // Vector2 adjustedOffset = waveOffset + waveSpeed * Time.time;
                    adjustedOffset = new Vector2(
                        waveOffset.x + waveSpeed.x * Time.time,
                        waveOffset.y + waveSpeed.y * Time.time
                    );

                    // height = Mathf.Sin(
                    //     (x * waveScale.x + adjustedOffset.x) * periodX + 
                    //     (y * waveScale.y + adjustedOffset.y) * periodY
                    // ) * (enabled ? WaveHeight : 0);

                    // height = Mathf.Sin((x * waveScale.x + adjustedOffset.x) + periodX) *
                    // Mathf.Sin((y * waveScale.y + adjustedOffset.y) + periodY) * 
                    // (enabled ? WaveHeight : 0);  

                    height = Mathf.Sin((x * waveScale.x / 10 + adjustedOffset.x) * Mathf.PI + 1) *
                             Mathf.Sin((y * waveScale.y / 10 + adjustedOffset.y) * Mathf.PI + 1);

                    // height = Mathf.Sin((x * waveScale.x + adjustedOffset.x) * periodX) *
                    // Mathf.Sin((y * waveScale.y + adjustedOffset.y) * periodY) * 
                    // (enabled ? WaveHeight : 0);

                    // height = Mathf.Sin(((x + adjustedOffset.x) * waveScale.x) * periodX) *
                    // Mathf.Sin(((y + adjustedOffset.y) * waveScale.y) * periodY) * 
                    // (enabled ? WaveHeight : 0);   
                }
                else if (waveType == Type.pointed)
                {
                    // Vector2 adjustedOffset = waveOffset + waveSpeed * Time.time;
                    adjustedOffset = new Vector2(
                        waveOffset.x + waveSpeed.x * Time.time,
                        waveOffset.y + waveSpeed.y * Time.time
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
                else if (waveType == Type.ripple)
                {
                    //https://www.wolframalpha.com/input/?i=sin%283+*+%28+sqrt%28x*x+%2B+y*y%29+%2F+10+%29+*+pi*2+%2B+1%29%2C+x%3D-5+to+5+y%3D-5+to5

                    // Vector2 offset = Time.time * waveSpeed + waveOffset;
                    // Vector2 delta = new Vector2(
                    //     x * waveScale.x + offset.x,
                    //     y * waveScale.y + offset.y
                    // );
                    // //Math.abs((Math.sin(WAVE_COUNT * (Math.sqrt((x) * (x) + (y) * (y)) / WIDTH) * RAD + phaseShift) + 1)
                    // height = Mathf.Sin(3 * (Mathf.Sqrt(delta.x * delta.x + delta.y * delta.y) / 10) * Mathf.PI * 2 + 1);

                    //float distance = 5;
                    // float spread = Mathf.Pow(Mathf.Sqrt(2 * Mathf.PI), -Mathf.Pow(y / distance, 2) / 2) * 10;


                    // float spread = 
                    //     (Mathf.Pow(Mathf.Sqrt(2 * Mathf.PI), -Mathf.Pow(x / distance, 2) / 2)
                    //      + Mathf.Pow(Mathf.Sqrt(2 * Mathf.PI), -Mathf.Pow(y / distance, 2) / 2))
                    //     / waveScale.y;

                    // height = spread * WaveHeight; 

                    //waveScale.y, waveSpeed.y, and waveOffset.y not used.
                    waveScale.y = waveScale.x;
                    waveSpeed.y = waveSpeed.x;
                    waveOffset.y = waveOffset.x;

                    adjustedOffset = new Vector2(
                        waveOffset.x + waveSpeed.x * Time.time,
                        waveOffset.y + waveSpeed.y * Time.time
                    );
                    Vector2 delta = new Vector2(
                        (x - position.x) * waveScale.x,
                        (y - position.z) * waveScale.x
                    );

                    //Formula: Math.abs((Math.sin(WAVE_COUNT * (Math.sqrt((x) * (x) + (y) * (y)) / WIDTH) * RAD + phaseShift) + 1)
                    height = Mathf.Sin(Mathf.Sqrt(delta.x * delta.x + delta.y * delta.y) / 10 * Mathf.PI * 2 +
                                       adjustedOffset.x);
                }
                else if (waveType == Type.bell)
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
                else if (waveType == Type.random)
                {
                    Vector2 offset = Time.time * waveSpeed + waveOffset;
                    ;
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
                else if (waveType == Type.custom)
                {
                    adjustedOffset.x = waveOffset.x + waveSpeed.x * Time.time;
                    adjustedOffset.y = waveOffset.y + waveSpeed.y * Time.time;
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
                        (customWave.Evaluate(adjustedPosition.x) +
                         customWave.Evaluate(adjustedPosition.y)) / 2;
                }
                // Profiler.EndSample();
            }

            // Profiler.BeginSample("Apply spread and interpolation");
            height *= ignoreInterpolation ? 1 : adjustedWaveHeight;
            height *= spreadMultiplier;
            // Profiler.EndSample();

            return height;
        }

        // public bool IsEnabled => waveEnabled && enabled && gameObject.activeInHierarchy;

        /// <summary>
        ///   <para>Weather the wave is enabled or not. We use this instead of "Component.enabled" because when disabled
        ///   we interpolate the wave height back to 0, if interpolation is enabled.</para>
        /// </summary>
        public bool WaveEnabled
        {
            get => waveEnabled;
            set => waveEnabled = value;
        }

        /// <summary>
        ///   <para>The type of wave curve algorithm.</para>
        /// </summary>
        public Type WaveType
        {
            get => waveType;
            set => waveType = value;
        }

        /// <summary>
        ///   <para>The height of the wave from origin to to peak. The lenght from valley to peak is double this for some
        ///   wave types.</para>
        /// </summary>
        public float WaveHeight
        {
            get => waveHeight;
            set => waveHeight = value;
        }

        /// <summary>
        ///   <para></para>
        /// </summary>
        public Vector2 WaveScale
        {
            get => waveScale;
            set => waveScale = value;
        }

        /// <summary>
        ///   <para></para>
        /// </summary>
        public Vector2 WaveOffset
        {
            get => waveOffset;
            set => waveOffset = value;
        }

        /// <summary>
        ///   <para></para>
        /// </summary>
        public Vector2 WaveSpeed
        {
            get => waveSpeed;
            set => waveSpeed = value;
        }

        /// <summary>
        ///   <para></para>
        /// </summary>
        public bool Interpolate
        {
            get => interpolate;
            set => interpolate = value;
        }

        /// <summary>
        ///   <para></para>
        /// </summary>
        public bool Fluctuate
        {
            get => fluctuate;
            set => fluctuate = value;
        }

        /// <summary>
        ///   <para></para>
        /// </summary>
        public float FluctuatePeriodTime
        {
            get => fluctuatePeriodTime;
            set => fluctuatePeriodTime = value;
        }

        /// <summary>
        ///   <para></para>
        /// </summary>
        public float FluctuateOffset
        {
            get => fluctuateOffset;
            set => fluctuateOffset = value;
        }
    }
}