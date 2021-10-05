// HiFiAudioAPIData.cs

using SimpleJSON;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiFi {

/// <summary name="AudioAPIDataChanges">
/// Helper class for tracking changes in OutgoingAudioAPIData.
/// <summary>
[System.Serializable]
public class AudioAPIDataChanges {
    public float? x; // position.x
    public float? y; // position.y
    public float? z; // position.z
    public float? W; // orientation.w
    public float? X; // orientation.x
    public float? Y; // orientation.y
    public float? Z; // orientation.z
    public float? T; // volumeThreshold (aka noiseThreshold)
    public float? g; // gain
    public float? a; // attenutation
    public float? r; // rolloff
    public Dictionary<string, float> V; // map of gains for other Peer aduio { visitIdHash : gain, ... }

    public bool IsEmpty() {
        return x == null && y == null && z == null
            && W == null && X == null && Y == null && Z == null
            && T == null && g == null && a == null && r == null
            && V == null;
    }

    public string ToWireFormattedJsonString() {
        JSONNode obj = new JSONObject();
        // Note: we quantize position to millimeters and store as int
        const float POSITION_TO_WIRE_SCALE_FACTOR = 1000.0f;
        if (x.HasValue) {
            obj["x"] = (int)(x.Value * POSITION_TO_WIRE_SCALE_FACTOR);
        }
        if (y.HasValue) {
            obj["y"] = (int)(y.Value * POSITION_TO_WIRE_SCALE_FACTOR);
        }
        if (z.HasValue) {
            obj["z"] = (int)(z.Value * POSITION_TO_WIRE_SCALE_FACTOR);
        }
        // Note: we scale orientation up by 1000
        // but do not quantize it.  This is probably a bug.
        // We might fix it someday although if we do...
        // we'll probably want more than 3 significant digits.
        const float ORIENTATION_TO_WIRE_SCALE_FACTOR = 1000.0f;
        if (W.HasValue) {
            obj["W"] = W.Value * ORIENTATION_TO_WIRE_SCALE_FACTOR;
        }
        if (X.HasValue) {
            obj["X"] = X.Value * ORIENTATION_TO_WIRE_SCALE_FACTOR;
        }
        if (Y.HasValue) {
            obj["Y"] = Y.Value * ORIENTATION_TO_WIRE_SCALE_FACTOR;
        }
        if (Z.HasValue) {
            obj["Z"] = Z.Value * ORIENTATION_TO_WIRE_SCALE_FACTOR;
        }

        if (T.HasValue) {
            if (Single.IsNaN(T.Value)) {
                // 'T' (volumeThreshold) has a special meaning when NaN, but we pack "null"
                obj["T"] = null;
            } else {
                obj["T"] = T.Value;
            }
        }
        if (g.HasValue) {
            obj["g"] = g.Value;
        }
        if (a.HasValue) {
            if (Single.IsNaN(a.Value)) {
                // 'a' (attenuation) has a special meaning when NaN, but we pack "null"
                obj["a"] = null;
            } else {
                obj["a"] = a.Value;
            }
        }
        if (r.HasValue) {
            if (Single.IsNaN(r.Value)) {
                // 'r' (rolloff) has a special meaning when NaN, but we pack "null"
                obj["r"] = null;
            } else {
                obj["r"] = r.Value;
            }
        }

        if (V != null) {
            JSONNode custom_gains = new JSONObject();
            foreach (KeyValuePair<string, float> kvp in V) {
                custom_gains[kvp.Key] = kvp.Value;
            }
            obj["V"] = custom_gains;
        }
        return obj.ToString();
    }
}

/// <summary name="OutgoingAudioAPIData">
/// User specific data sent to HiFi Spatial Audio Service.
/// </summary>
/// <seealso cref="IncomingAudioAPIData"/>
public class OutgoingAudioAPIData {
    /// <summary>
    /// Position (3D cartesian vector) of User in HiFi Spatial Audio Space.
    /// </summary>
    /// <remarks>
    /// The HiFi Spatial Audio coordinate system is right-handed Cartesian
    /// where, given an identity orientation, "forward" points along negative
    /// z-axis and "up" along positive y-axis.
    /// </remarks>
    public Vector3 position;

    /// <summary>
    /// Rotation (in quaternion format) of User in HiFi Spatial Audio Space.
    /// </summary>
    /// <remarks>
    /// The HiFi Spatial Audio coordinate system is right-handed Cartesian
    /// where, given an identity orientation, "forward" points along negative
    /// z-axis and "up" along positive y-axis.
    /// </remarks>
    public Quaternion orientation;

    /// <summary>
    /// Noise gate threshold (range = [-96, 0] db) of User's audio in the HiFi
    /// Spatial Audio Space.
    /// </summary>
    /// <remarks>
    /// A volume level below the volumeThreshold will be considered background
    /// noise and will be smoothly gated off.  The value is specified in dbFS
    /// (decibels relative to full scale) with values between -96.0 and 0.0.
    /// If never explicitly set, or set to NaN (Not a Number), the server will
    /// use a default value of -40.0.  Setting volumeThreshold to 0.0 will
    /// effectively mute the User for all listeners at the server's spatial
    /// mixing stage.
    /// </remarks>
    public float volumeThreshold;

    /// <summary>
    /// Gain (loudness) (range = [0,1]) for User's audio in the HiFi Spatial
    /// Audio Space.
    /// </summary>
    /// <remarks>
    /// Gain ranges from 0.0 (mute) to 1.0 (full volume strength).
    /// </remarks>
    public float hiFiGain;

    /// <summary name="userAttenuation">
    /// Amount of attenuation (range = [0,1]) of sound volume as it travels
    /// over distance.  Low values mean less attenuation.
    /// </summary>
    /// <remarks>
    /// By default, there is a global attenuation value (set for a given Space)
    /// that applies to all Users therein. This default Space attenuation is
    /// usually 0.5, which represents a reasonable approximation of a
    /// real-world fall-off in sound over distance.  Lower numbers represent
    /// less attenuation (i.e. sound travels farther); higher numbers represent
    /// more attenuation (i.e. sound drops off more quickly).  A value of NaN
    /// (Not a Number) means "defer to server default".
    ///
    /// When setting this value for an individual User, the following holds:
    ///   - Positive numbers should be between 0 and 1, and they represent a
    ///   logarithmic attenuation. This range is recommended, as it is more
    ///   natural sounding.  Smaller numbers represent less attenuation, so a
    ///   number such as 0.2 can be used to make a particular User's audio
    ///   travel farther than other Users', for instance in "amplified" concert
    ///   type settings. Similarly, an extremely small non-zero number (e.g.
    ///   0.00001) can be used to effectively turn off attenuation for a given
    ///   User within a reasonably sized Space, resulting in a "broadcast mode"
    ///   where the User can be heard throughout most of the Space regardless
    ///   of their location relative to other Users. Note: The actual value "0"
    ///   is used internally to represent the default; for setting minimal
    ///   attenuation, small non-zero numbers should be used instead.
    ///   - Negative attenuation numbers are used to represent linear
    ///   attenuation, and are a somewhat artificial, non-real-world concept.
    ///   However, this setting can be used as a blunt tool to easily test
    ///   attenuation, and tune it aggressively in extreme circumstances.  When
    ///   using linear attenuation, the setting is the distance in meters at
    ///   which the audio becomes totally inaudible.
    ///
    /// WARNING: a userAttenuation of 0.0 will also be interpreted by the
    /// server to mean "defer to default" however this behavior is scheduled to
    /// change to mean "zero attenuation".  Until advised otherwise: don't use
    /// zero userAttenuation.
    /// </remarks>
    /// <seealso cref="userRolloff"/>
    public float userAttenuation;

    /// <summary name="userRolloff">
    /// Approximate distance at which audio becomes muffled due to filtering of
    /// higher frequencies.
    /// </summary>
    /// <remarks>
    /// This value represents the progressive high frequency roll-off in
    /// meters, a measure of how the higher frequencies in a User's sound are
    /// dampened as the User gets further away.  A value of NaN means "defer to
    /// server default".
    ///
    /// By default, there is a global roll-off value (set for a given Space),
    /// currently 16 meters, which applies to all Users in a Space.  This value
    /// represents the distance at which the "knee" of the low-pass filter is
    /// at 1kHz. Values in the range of 12 to 32 meters provide a more
    /// "enclosed" sound, in which high frequencies tend to be dampened over
    /// distance as they are in the real world. Generally changes to roll-off
    /// values should be made for the entire Space rather than for individual
    /// Users, but extremely high values (e.g. 99999) should be used in
    /// combination with "broadcast mode"-style userAttenuation settings to
    /// cause the broadcasted voice to sound crisp and "up close" even at very
    /// large distances.
    ///
    /// WARNING: a userRolloff of 0.0 will also be interpreted by the
    /// server to mean "defer to default" however this behavior is scheduled to
    /// change to mean "immediate rolloff" which would effectively mute the User.
    /// Don't use zero userRolloff.
    /// </remarks>
    /// <seealso cref="userAttenuation"/>
    public float userRolloff;

    /// <summary name="otherUserGains">
    /// Per Peer gain adjustments the server should apply when mixing audio for
    /// this User.
    /// </summary>
    /// <remarks>
    /// This is a map between visitIdHash and custom gain setting:
    ///   { visitIdhash : gain, ... }
    /// The value of gain is in range [0,10] with a default value of 1.  Higher
    /// gain makes the Peer louder, lower gain quieter.  A gain of 0 will
    /// silence the Peer in the mixed audio sent from the server to the User.
    /// </remarks>
    public Dictionary<string, float> otherUserGains;

    public OutgoingAudioAPIData() {
        position = new Vector3(0.0f, 0.0f, 0.0f);
        orientation = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
        const float DEFAULT_VOLUME_THRESHOLD = Single.NaN; // NaN means "defer to server default"
        const float DEFAULT_HIFI_GAIN = 1.0f;
        const float DEFAULT_USER_ATTENUATION = Single.NaN; // NaN means "defer to server default"
        const float DEFAULT_USER_ROLLOFF = Single.NaN; // NaN means "defer to server default"
        volumeThreshold = DEFAULT_VOLUME_THRESHOLD;
        hiFiGain = DEFAULT_HIFI_GAIN;
        userAttenuation = DEFAULT_USER_ATTENUATION;;
        userRolloff = DEFAULT_USER_ROLLOFF;
        otherUserGains = new Dictionary<string, float>();
    }

    /// <summary>
    /// Return a deep copy of this OutgoingAudioAPIData.
    /// </summary>
    public OutgoingAudioAPIData DeepCopy() {
        OutgoingAudioAPIData other = new OutgoingAudioAPIData();
        other.position = new Vector3(position.x, position.y, position.z);
        other.orientation = new Quaternion(orientation.x, orientation.y, orientation.z, orientation.w);
        other.volumeThreshold = volumeThreshold;
        other.hiFiGain = hiFiGain;
        other.userAttenuation = userAttenuation;
        other.userRolloff = userRolloff;
        foreach (string key in otherUserGains.Keys) {
            other.otherUserGains.Add(key, otherUserGains[key]);
        }
        return other;
    }

    /// <summary>
    /// Update the values of this OutgoingAudioAPIData and store the changed
    /// values in AudioAPIDataChanges.
    /// </summary>
    /// <returns>
    /// AudioAPIDataChanges with all changed values.
    /// </returns>
    public AudioAPIDataChanges ApplyAndGetChanges(OutgoingAudioAPIData other) {
        AudioAPIDataChanges changes = new AudioAPIDataChanges();

        if (other == null) {
            return changes;
        }
        if (position != other.position) {
            if (position.x != other.position.x) {
                changes.x = other.position.x;
                position.x = other.position.x;
            }
            if (position.y != other.position.y) {
                changes.y = other.position.y;
                position.y = other.position.y;
            }
            if (position.z != other.position.z) {
                changes.z = other.position.z;
                position.z = other.position.z;
            }
        }

        if (orientation != other.orientation) {
            Quaternion Q = other.orientation;
            if (orientation.w != other.orientation.w) {
                changes.W = other.orientation.w;
                orientation.w = other.orientation.w;
            }
            if (orientation.x != other.orientation.x) {
                changes.X = other.orientation.x;
                orientation.x = other.orientation.x;
            }
            if (orientation.y != other.orientation.y) {
                changes.Y = other.orientation.y;
                orientation.y = other.orientation.y;
            }
            if (orientation.z != other.orientation.z) {
                changes.Z = other.orientation.z;
                orientation.z = other.orientation.z;
            }
        }

        bool param_is_nan = Single.IsNaN(volumeThreshold);
        bool other_param_is_nan = Single.IsNaN(other.volumeThreshold);
        if (param_is_nan != other_param_is_nan
                || (!param_is_nan && volumeThreshold != other.volumeThreshold))
        {
            changes.T = other.volumeThreshold;
            volumeThreshold = other.volumeThreshold;
        }

        if (hiFiGain != other.hiFiGain) {
            changes.g = other.hiFiGain;
            hiFiGain = other.hiFiGain;
        }

        param_is_nan = Single.IsNaN(userAttenuation);
        other_param_is_nan = Single.IsNaN(other.userAttenuation);
        if (param_is_nan != other_param_is_nan
                || (!param_is_nan && userAttenuation != other.userAttenuation))
        {
            changes.a = other.userAttenuation;
            userAttenuation = other.userAttenuation;
        }

        param_is_nan = Single.IsNaN(userRolloff);
        other_param_is_nan = Single.IsNaN(other.userRolloff);
        if (param_is_nan != other_param_is_nan
                || (!param_is_nan && userRolloff != other.userRolloff))
        {
            changes.r = other.userRolloff;
            userRolloff = other.userRolloff;
        }

        Dictionary<string, float> V = new Dictionary<string, float>();
        const float DEFAULT_GAIN = 1.0f;
        foreach (KeyValuePair<string, float> kvp in other.otherUserGains) {
            string key = kvp.Key;
            float gain = kvp.Value;
            if (otherUserGains.ContainsKey(key)) {
                // an existing gain override is changing
                if (gain != otherUserGains[key]) {
                    V[key] = gain;
                    if (gain == DEFAULT_GAIN) {
                        // overrides at default level can be removed from tracking
                        otherUserGains.Remove(key);
                    } else {
                        otherUserGains[key] = gain;
                    }
                }
            } else if (gain != DEFAULT_GAIN) {
                // a gain override is being added
                V[key] = gain;
                otherUserGains[key] = gain;
            }
        }
        // TODO: when peers are deleted we should check for, and clear out,
        // corresponding entries in otherUserGains.
        if (V.Count > 0) {
            changes.V = V;
        }

        return changes;
    }
}

/// <summary name="IncomingAudioAPIData">
/// Data received from HiFi Spatial Audio Service.
/// </summary>
/// <seealso cref="OutgoingAudioAPIData"/>
public class IncomingAudioAPIData {
    /// <summary>
    /// Position (3D cartesian vector) of User in HiFi Spatial Audio Space.
    /// </summary>
    /// <remarks>
    /// The HiFi Spatial Audio coordinate system is right-handed Cartesian
    /// where, given an identity orientation, "forward" points along negative
    /// z-axis and "up" along positive y-axis.
    /// </remarks>
    public Vector3 position;

    /// <summary>
    /// Rotation (in quaternion format) of User in HiFi Spatial Audio Space.
    /// </summary>
    /// <remarks>
    /// The HiFi Spatial Audio coordinate system is right-handed Cartesian
    /// where, given an identity orientation, "forward" points along negative
    /// z-axis and "up" along positive y-axis.
    /// </remarks>
    public Quaternion orientation;

    /// <summary>
    /// The User's "publicly visisble name" extracted from the JWT provided at login.
    /// </summary>
    /// <remarks>
    /// The intention is for providedUserID to be the visible name of the User.The HiFi Spatial Audio Service
    /// exracts it from the JWT used at login and streams it to peers but does not otherwise use it.
    /// </remarks>
    public string providedUserID;

    /// <summary>
    /// The hash of the User's random session UUID.
    /// </summary>
    /// <remarks>
    /// A unique public identifier for the User's session.
    /// </remarks>
    public string visitIdHash;

    /// <summary>
    /// True if User is streaming stereo input to server.
    /// </summary>
    public bool isStereo;

    /// <summary>
    /// The current volume of the User in decibels.
    /// </summary>
    public float volumeDecibels;

    public IncomingAudioAPIData() {
        position = new Vector3(0.0f, 0.0f, 0.0f);
        orientation = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
        providedUserID = "";
        visitIdHash = "";
        isStereo = false;
        volumeDecibels = 0.0f;
    }

    public IncomingAudioAPIData DeepCopy() {
        IncomingAudioAPIData other = new IncomingAudioAPIData();
        other.position = new Vector3(position.x, position.y, position.z);
        other.orientation = new Quaternion(orientation.x, orientation.y, orientation.z, orientation.w);

        other.providedUserID = providedUserID;
        other.visitIdHash = visitIdHash;
        other.isStereo = isStereo;
        other.volumeDecibels = volumeDecibels;
        return other;
    }

    /// <summary>
    /// Apply a set of changes (in abbreviated JSON format) onto an existing
    /// IncomingAudioAPIData instance.
    /// </summary>
    /// <remarks>
    /// The HiFi Spatial Audio Service sends partial updates: only the changed
    /// fields are listed.  This method integrates the changes onto the
    /// previous values.
    /// </remarks>
    /// <returns>
    /// True if any field was updated, else False.
    /// </remarks>
    public bool ApplyWireFormattedJson(JSONNode obj) {
        // the key mappings are:
        //   J = providedUserID
        //   e = visitIdHash
        //   s = isStereo
        //   v = volumeDecibels
        //   x = position.x
        //   y = position.y
        //   z = position.z
        //   W = orientation.W
        //   X = orientation.X
        //   Y = orientation.y
        //   Z = orientation.z
        const float POSITION_FROM_WIRE_SCALE_FACTOR = 1.0f / 1000.0f;
        const float ORIENTATION_FROM_WIRE_SCALE_FACTOR = 1.0f / 1000.0f;
        bool somethingChanged = false;
        foreach (KeyValuePair<string, JSONNode> kvp in (JSONObject)obj) {
            if (kvp.Key == "J" && providedUserID != kvp.Value) {
                providedUserID = kvp.Value;
                somethingChanged = true;
            } else if (kvp.Key == "e" && visitIdHash != kvp.Value) {
                visitIdHash = kvp.Value;
                somethingChanged = true;
            } else if (kvp.Key == "e" && isStereo != kvp.Value) {
                isStereo = kvp.Value;
                somethingChanged = true;
            } else if (kvp.Key == "v" && volumeDecibels != kvp.Value) {
                volumeDecibels = kvp.Value;
                somethingChanged = true;
            } else if (kvp.Key == "x") {
                float x = kvp.Value.AsFloat * POSITION_FROM_WIRE_SCALE_FACTOR;
                if (position.x != x) {
                    position.x = x;
                    somethingChanged = true;
                }
            } else if (kvp.Key == "y") {
                float y = kvp.Value.AsFloat * POSITION_FROM_WIRE_SCALE_FACTOR;
                if (position.y != y) {
                    position.y = y;
                    somethingChanged = true;
                }
            } else if (kvp.Key == "z") {
                float z = kvp.Value.AsFloat * POSITION_FROM_WIRE_SCALE_FACTOR;
                if (position.z != z) {
                    position.z = z;
                    somethingChanged = true;
                }
            } else if (kvp.Key == "W") {
                float W = kvp.Value.AsFloat * ORIENTATION_FROM_WIRE_SCALE_FACTOR;
                if (orientation.w != W) {
                    orientation.w = W;
                    somethingChanged = true;
                }
            } else if (kvp.Key == "X") {
                float X = kvp.Value.AsFloat * ORIENTATION_FROM_WIRE_SCALE_FACTOR;
                if (orientation.x != X) {
                    orientation.x = X;
                    somethingChanged = true;
                }
            } else if (kvp.Key == "Y") {
                float Y = kvp.Value.AsFloat * ORIENTATION_FROM_WIRE_SCALE_FACTOR;
                if (orientation.y != Y) {
                    orientation.y = Y;
                    somethingChanged = true;
                }
            } else if (kvp.Key == "Z") {
                float Z = kvp.Value.AsFloat * ORIENTATION_FROM_WIRE_SCALE_FACTOR;
                if (orientation.z != Z) {
                    orientation.z = Z;
                    somethingChanged = true;
                }
            }
        }
        return somethingChanged;
    }

    /// <summary>
    /// For debug purposes
    /// </summary>
    public string ToWireFormattedJsonString() {
        // the key mappings are:
        //   J = providedUserID
        //   e = visitIdHash
        //   s = isStereo
        //   v = volumeDecibels
        //   x = position.x
        //   y = position.y
        //   z = position.z
        //   W = orientation.W
        //   X = orientation.X
        //   Y = orientation.y
        //   Z = orientation.z
        JSONNode obj = new JSONObject();
        obj["J"] = providedUserID;
        obj["e"] = visitIdHash;
        obj["s"] = isStereo;
        obj["v"] = volumeDecibels;
        obj["x"] = position.x;
        obj["y"] = position.y;
        obj["z"] = position.z;
        obj["W"] = orientation.w;
        obj["X"] = orientation.x;
        obj["Y"] = orientation.y;
        obj["Z"] = orientation.z;
        return obj.ToString();
    }
}

} // namespace HiFi
