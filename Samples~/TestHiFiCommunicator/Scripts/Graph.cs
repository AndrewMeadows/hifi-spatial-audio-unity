using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws a basic oscilloscope type graph in a GUI.Window()
/// </summary>
public class Graph : MonoBehaviour {

    class DataRing {
        List<float> _ring;
        int _numValues = 0;
        int _head = 0;
        int _version = 0;

        public int Count {
            get {
                return _numValues;
            }
        }

        public int Capacity {
            get {
                return _ring.Count;
            }
        }

        public int Version {
            internal set { }
            get {
                return _version;
            }
        }

        public DataRing(int size) {
            // preallocate the List
            _ring = new List<float>(size);
            for (int i = 0; i < size; i++) {
                _ring.Add(0.0f);
            }
            _numValues = 0;
            _head = 0;
        }

        public void PushBack(float datum) {
            int ring_length = _ring.Count;
            int tail = (_head - _numValues + ring_length) % ring_length;
            _ring[_head] = datum;
            _head = (_head + 1) % ring_length;
            if (_numValues < ring_length) {
                _numValues++;
            }
            _version++;
        }

        public float GetValue(int i) {
            // i is offset from tail
            int ring_length = _ring.Count;
            return _ring[(_head - _numValues + (i % ring_length) + ring_length) % ring_length];
        }
    }

    class Channel {
        DataRing _ring; // raw data
        List<float> _buffer; // drawable copy of data
        int _numValuesInBuffer;
        float _minValue;
        float _maxValue;
        int _lastVersion;
        Color _color = Color.green;

        /// Mode can be 'raw' (the default) or 'diff'
        /// 'raw' mode just stores the number
        /// 'diff' mode is useful for seeing changes in an otherwise monotonically increasing value
        public string Mode {
            set {
                _diffMode = (value == "diff");
            }
            get {
                return _diffMode ? "diff" : "raw";
            }
        }
        bool _diffMode = false;
        float _lastValue = 0.0f;

        public Color LineColor {
            set {
                _color = value;
            }
            get {
                return _color;
            }
        }

        public float MaxValue {
            get { return _maxValue; }
        }

        public Channel(int size) {
            _ring = new DataRing(size);
            InitBuffer(size);
            _minValue = System.Single.MaxValue;
            _maxValue = System.Single.MinValue;
            _lastVersion = 0;
        }

        void InitBuffer(int size) {
            _buffer = new List<float>(size);
            for (int i = 0; i < size; i++) {
                _buffer.Add(0.0f);
            }
            _numValuesInBuffer = 0;
        }

        public void AddValue(float datum) {
            if (_diffMode) {
                float diff = datum - _lastValue;
                _lastValue = datum;
                _ring.PushBack(diff);
            } else {
                _ring.PushBack(datum);
            }
        }

        public void CopyDataToBufferIfNecessary() {
            if (_lastVersion != _ring.Version) {
                for (int i = 0; i < _ring.Count; i++) {
                    float d = _ring.GetValue(i);
                    _buffer[i] = d;
                    if (d < _minValue) {
                        _minValue = d;
                    }
                    if (d > _maxValue) {
                        _maxValue = d;
                    }
                }
                _numValuesInBuffer = _ring.Count;
                _lastVersion = _ring.Version;
            }
        }

        public void ScaleBuffer(float new_min, float new_max) {
            float denominator = (_maxValue - _minValue);
            if (denominator == 0.0f) {
                denominator = 1.0f;
            }
            float scale = (new_max - new_min) / denominator;
            for (int i = 0; i < _numValuesInBuffer; i++) {
                _buffer[i] = (_buffer[i] - _minValue) * scale + new_min;
            }
        }

        public void Resize(int new_size) {
            if (new_size != _ring.Capacity) {
                CopyDataToBufferIfNecessary();
                _ring = new DataRing(new_size);
                int num_values = System.Math.Min(new_size, _numValuesInBuffer);
                int i = System.Math.Max(0, _numValuesInBuffer - num_values);
                while (i < num_values) {
                    _ring.PushBack(_buffer[i]);
                }
                InitBuffer(new_size);
                _lastVersion = 0;
            }
        }

        public void Draw(float width, float height, float border) {
            // Draw the lines of the graph
            GL.Begin(GL.LINES);
            GL.Color(_color);

            float dx = (width - 2.0f * border) / (float)(_numValuesInBuffer - 1);
            for (int i = 0; i < _numValuesInBuffer - 1; i++) {
                float x = border + dx * (float)(i);
                float y1 = height - border - _buffer[i];
                float y2 = height - border - _buffer[i + 1];
                GL.Vertex3(x, y1, 0);
                GL.Vertex3(x + dx, y2, 0);
            }
            GL.End();
        }
    }

    Material _mat;
    Dictionary<string, Channel> _channelMap;
    bool _showWindow = true;

    public Rect WindowRect {
        set {
            if (value != null) {
                _windowRect = value;
                int width = (int)(_windowRect.width) - 2 * _borderWidth;
                foreach (Channel channel in _channelMap.Values) {
                    channel.Resize(width);
                }
                _graphHeight = (int)(_windowRect.height) - 2 * _borderWidth;
            }
        }
        get {
            return _windowRect;
        }
    }
    Rect _windowRect;
    int _graphHeight;
    int _version = 0;
    int _lastVersion = 0;

    const int _borderWidth = 4;

    void Awake() {
        _mat = new Material(Shader.Find("Hidden/Internal-Colored"));
        _channelMap = new Dictionary<string, Channel>();
    }

    public void AddChannel(string key, string mode, Color color) {
        if (!_channelMap.ContainsKey(key)) {
            int width = (int)(_windowRect.width) - 2 * _borderWidth;
            Channel channel = new Channel(width);
            channel.Mode = mode;
            channel.LineColor = color;
            _channelMap[key] = channel;
        }
    }

    public void AddValue(string key, float datum) {
        if (_channelMap.ContainsKey(key)) {
            _channelMap[key].AddValue(datum);
            _version++;
        }
    }

    void Start() {
    }

    void FixedUpdate() {
    }

    private void OnGUI() {
        if (_windowRect != null) {
            // Create a GUI.toggle to show graph window
            float x = _windowRect.x - 10;
            float y = _windowRect.y - 10;
            _showWindow = GUI.Toggle(new Rect(x, y, 100, 20), _showWindow, "Show Graph");

            if (_showWindow) {
                if (_lastVersion != _version) {
                    foreach (Channel channel in _channelMap.Values) {
                        channel.CopyDataToBufferIfNecessary();
                        channel.ScaleBuffer(0, _graphHeight);
                    }
                    _lastVersion = _version;
                }
                _windowRect = GUI.Window(0, _windowRect, DrawGraph, "");
            }
        }
    }

    void DrawGraph(int windowID) {
        // Make Window Draggable
        //GUI.DragWindow(new Rect(0, 0, 10000, 10000));
        GUI.DragWindow(_windowRect);

        float border = (float)_borderWidth;
        // Draw the graph in the repaint cycle
        if (Event.current.type == EventType.Repaint) {
            GL.PushMatrix();

            GL.Clear(true, false, Color.black);
            _mat.SetPass(0);

            // Draw a black background Quad
            GL.Begin(GL.QUADS);
            GL.Color(Color.black);
            GL.Vertex3(border, border, 0.0f);
            GL.Vertex3(_windowRect.width - border, border, 0.0f);
            GL.Vertex3(_windowRect.width - border, _windowRect.height - border, 0.0f);
            GL.Vertex3(border, _windowRect.height - border, 0.0f);
            GL.End();

            foreach (Channel channel in _channelMap.Values) {
                channel.Draw(_windowRect.width, _windowRect.height, border);
            }

            GL.PopMatrix();
        }
        // Add labels
        int num_channels = _channelMap.Count;
        float dy = (_windowRect.width - 2.0f * border) / (float)(num_channels);
        int i = 0;
        foreach (KeyValuePair<string, Channel> kvp in _channelMap) {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = kvp.Value.LineColor;
            float y = border + (float)i * dy;
            GUI.Label(new Rect(y, border, 100, 20), kvp.Value.MaxValue.ToString(), style);
            GUI.Label(new Rect(y, _graphHeight + border- 20, 200, 20), kvp.Key, style);
            i++;
        }
    }
}
