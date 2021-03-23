// HiFiAudioAPIData.cs

using SimpleJSON;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiFi {

[System.Serializable]
public class AudioAPIDataChanges {
    public float? x; // position.x
    public float? y; // position.y
    public float? z; // position.z
    public float? W; // orientation.w
    public float? X; // orientation.x
    public float? Y; // orientation.y
    public float? Z; // orientation.z
    public float? T; // noiseThreshold
    public float? g; // gain
    public float? a; // attenutation
    public float? r; // rolloff

    public bool IsEmpty() {
        return x == null && y == null && z == null
            && W == null && X == null && Y == null && Z == null
            && T == null && g == null && a == null && r == null;
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
            obj["T"] = T.Value;
        }
        if (g.HasValue) {
            obj["g"] = g.Value;
        }
        if (a.HasValue) {
            obj["a"] = a.Value;
        }
        if (r.HasValue) {
            obj["r"] = r.Value;
        }
        return obj.ToString();
    }
}


/**
 * Instantiations of this class contain all of the data that is possible to **send to AND receive from** the High Fidelity Audio API Server.
 * All member data inside this `class` can be sent to the High Fidelity Audio API Server. See below for more details.
 *
 * See {@link ReceivedHiFiAudioAPIData} for data that can't be sent to the Server, but rather can only be received from the Server (i.e. `volumeDecibels`).
 *
 * Member data of this class that is sent to the Server will affect the final mixed spatial audio for all listeners in the server's virtual space.
 */
public class OutgoingAudioAPIData {
    /**
     * ✔ The client sends `position` data to the server when `_transmitHiFiAudioAPIDataToServer()` is called.
     *
     * ✔ The server sends `position` data to all clients connected to a server during "peer updates".
     */
    public Vector3 _position;
    /**
     * ✔ The client sends `orientation` data to the server when `_transmitHiFiAudioAPIDataToServer()` is called.
     *
     * ✔ The server sends `orientation` data to all clients connected to a server during "peer updates".
     */
    public Quaternion _orientation;
    //Vector3 orientationEuler; // unsupported for Unity
    /**
     * A volume level below this value is considered background noise and will be smoothly gated off.
     * The floating point value is specified in dBFS (decibels relative to full scale) with values between -96 dB (indicating no gating)
     * and 0 dB. It is in the same decibel units as the VolumeDecibels component of UserDataSubscription.
     */
    public float _volumeThreshold;
    /**
     * This value affects how loud UserA will sound to UserB at a given distance in 3D space.
     * This value also affects the distance at which UserA can be heard in 3D space.
     * Higher values for UserA means that UserA will sound louder to other users nearby, and it also means that UserA will be audible from a greater distance.
     */
    public float _hiFiGain;
    /**
     * This value affects how far a user's sound will travel in 3D space, without affecting the user's loudness.
     * By default, there is a global attenuation value (set for a given space) that applies to all users in a space. This default space
     * attenuation is usually 0.5, which represents a reasonable approximation of a real-world fall-off in sound over distance.
     * Lower numbers represent less attenuation (i.e. sound travels farther); higher numbers represent more attenuation (i.e. sound drops
     * off more quickly).
     *
     * When setting this value for an individual user, the following holds:
     *   - Positive numbers should be between 0 and 1, and they represent a logarithmic attenuation. This range is recommended, as it is
     * more natural sounding.  Smaller numbers represent less attenuation, so a number such as 0.2 can be used to make a particular
     * user's audio travel farther than other users', for instance in "amplified" concert type settings. Similarly, an extremely
     * small non-zero number (e.g. 0.00001) can be used to effectively turn off attenuation for a given user within a reasonably
     * sized space, resulting in a "broadcast mode" where the user can be heard throughout most of the space regardless of their location
     * relative to other users. (Note: The actual value "0" is used internally to represent the default; for setting minimal attenuation,
     * small non-zero numbers should be used instead. See also "userRolloff" below.)
     *   - Negative attenuation numbers are used to represent linear attenuation, and are a somewhat artificial, non-real-world concept. However,
     * this setting can be used as a blunt tool to easily test attenuation, and tune it aggressively in extreme circumstances. When using linear
     * attenuation, the setting is the distance in meters at which the audio becomes totally inaudible.
     *
     * ✔ The client sends `userAttenuation` data to the server when `_transmitHiFiAudioAPIDataToServer()` is called.
     *
     * ❌ The server never sends `userAttenuation` data.
     */
    public float _userAttenuation;
    /**
     * @param userRolloff This value represents the progressive high frequency roll-off in meters, a measure of how the higher frequencies
     * in a user's sound are dampened as the user gets further away. By default, there is a global roll-off value (set for a given space), currently 16
     * meters, which applies to all users in a space. This value represents the distance for a 1kHz rolloff. Values in the range of
     * 12 to 32 meters provide a more "enclosed" sound, in which high frequencies tend to be dampened over distance as they are
     * in the real world. Generally changes to roll-off values should be made for the entire space rather than for individual users, but
     * extremely high values (e.g. 99999) should be used in combination with "broadcast mode"-style userAttenuation settings to cause the
     * broadcasted voice to sound crisp and "up close" even at very large distances.
     *
     * ✔ The client sends `userRolloff` data to the server when `_transmitHiFiAudioAPIDataToServer()` is called.
     *
     * ❌ The server never sends `userRolloff` data.
     */
    public float _userRolloff;

    public OutgoingAudioAPIData() {
        _position = new Vector3(0.0f, 0.0f, 0.0f);
        _orientation = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
        _volumeThreshold = 0.0f;
        _hiFiGain = 0.0f;
        _userAttenuation = 0.0f;
        _userRolloff = 16.0f;
    }

    public OutgoingAudioAPIData DeepCopy() {
        OutgoingAudioAPIData other = new OutgoingAudioAPIData();
        other._position = _position;
        other._orientation = _orientation;
        other._volumeThreshold = _volumeThreshold;
        other._hiFiGain = _hiFiGain;
        other._userAttenuation = _userAttenuation;
        other._userRolloff = _userRolloff;
        return other;
    }

    public AudioAPIDataChanges ApplyAndGetChanges(OutgoingAudioAPIData other) {
        AudioAPIDataChanges changes = new AudioAPIDataChanges();

        if (other == null) {
            return changes;
        }
        if (_position != other._position) {
            if (_position.x != other._position.x) {
                changes.x = other._position.x;
                _position.x = other._position.x;
            }
            if (_position.y != other._position.y) {
                changes.y = other._position.y;
                _position.y = other._position.y;
            }
            if (_position.z != other._position.z) {
                changes.z = other._position.z;
                _position.z = other._position.z;
            }
        }

        if (_orientation != other._orientation) {
            Quaternion Q = other._orientation;
            if (_orientation.w != other._orientation.w) {
                changes.W = other._orientation.w;
                _orientation.w = other._orientation.w;
            }
            if (_orientation.x != other._orientation.x) {
                changes.X = other._orientation.x;
                _orientation.x = other._orientation.x;
            }
            if (_orientation.y != other._orientation.y) {
                changes.Y = other._orientation.y;
                _orientation.y = other._orientation.y;
            }
            if (_orientation.z != other._orientation.z) {
                changes.Z = other._orientation.z;
                _orientation.z = other._orientation.z;
            }
        }


        if (_volumeThreshold != other._volumeThreshold) {
            changes.T = _volumeThreshold;
            _volumeThreshold = other._volumeThreshold;
        }

        if (_hiFiGain != other._hiFiGain) {
            changes.g = _hiFiGain;
            _hiFiGain = other._hiFiGain;
        }

        if (_userAttenuation != other._userAttenuation) {
            changes.a = _userAttenuation;
            _userAttenuation = other._userAttenuation;
        }

        if (_userRolloff != other._userRolloff) {
            changes.r = _userRolloff;
            _userRolloff = other._userRolloff;
        }
        return changes;
    }
}

/**
 * Instantiations of this class contain all of the data that is possible to **receive from** the High Fidelity Audio API Server.
 * See below for more details.
 *
 * See {@link OutgoingAudioAPIData} for data that can both be sent to and received from the Server (i.e. `position`).
 */
public class IncomingAudioAPIData : OutgoingAudioAPIData {
    /**
     * This User ID is an arbitrary string provided by an application developer which can be used to identify the user associated with a client.
     * We recommend that this `providedUserID` be unique across all users, however the High Fidelity API will not enforce uniqueness across clients for this value.
     */
    public string _providedUserID;
    /**
     * This string is a hashed version of the random UUID that is generated automatically.
     *
     * A connecting client sends this value as the `session` key inside the argument to the `audionet.init` command.
     *
     * It is used to identify a given client across a cloud of mixers and is guaranteed ("guaranteed" given the context of random UUIDS) to be unique.
     * Application developers should not need to interact with or make use of this value, unless they want to use it internally for tracking or other purposes.
     *
     * This value cannot be set by the application developer.
     */
    public string _hashedVisitID;
    /**
     * The current volume of the user in decibels.
     *
     * ❌ The client never sends `volumeDecibels` data to the server.
     *
     * ✔ The server sends `volumeDecibels` data to all clients connected to a server during "peer updates".
     */
    public float _volumeDecibels;

    public IncomingAudioAPIData() : base() {
        _providedUserID = "";
        _hashedVisitID = "";
        _volumeDecibels = 0.0f;
    }

    public new IncomingAudioAPIData DeepCopy() {
        IncomingAudioAPIData other = new IncomingAudioAPIData();
        other._position = _position;
        other._orientation = _orientation;
        other._volumeThreshold = _volumeThreshold;
        other._hiFiGain = _hiFiGain;
        other._userAttenuation = _userAttenuation;
        other._userRolloff = _userRolloff;

        other._providedUserID = _providedUserID;
        other._hashedVisitID = _hashedVisitID;
        other._volumeDecibels = _volumeDecibels;
        return other;
    }

    /**
     * IncomingAudioAPIData from Server to Client is packed on the wire
     * as a JSON string with abbreviated keys.
     * This method takes such a JSON object as argument,
     * translates its key-values into corresponding fields,
     * and updates those fields.
     * @param obj A JSON object with abbreviated keys corresponding to fields of the class.
     * @return Returns 'true' if any field was updated, else 'false'.
     */
    public bool ApplyWireFormattedJson(JSONNode obj) {
        // the key mappings are:
        //   J = _providedUserID
        //   e = _hashedVisitID
        //   v = _volumeDecibels
        //   x = Position.x
        //   y = Position.y
        //   z = Position.z
        //   W = Orientation.W
        //   X = Orientation.X
        //   Y = Orientation.y
        //   Z = Orientation.z
        bool somethingChanged = false;
        try {
            string userID = obj["J"].Value;
            if (_providedUserID != userID) {
                _providedUserID = userID;
                somethingChanged = true;
            }
        } catch (Exception) {
        }
        try {
            string hashedVisitID = obj["e"].Value;
            if (_hashedVisitID != hashedVisitID) {
                _hashedVisitID = hashedVisitID;
                somethingChanged = true;
            }
        } catch (Exception) {
        }
        try {
            float volume = obj["v"].AsFloat;
            if (_volumeDecibels != volume) {
                _volumeDecibels = volume;
                somethingChanged = true;
            }
        } catch (Exception) {
        }
        const float POSITION_FROM_WIRE_SCALE_FACTOR = 1.0f / 1000.0f;
        try {
            float x = obj["x"].AsFloat * POSITION_FROM_WIRE_SCALE_FACTOR;
            if (_position.x != x) {
                _position.x = x;
                somethingChanged = true;
            }
        } catch (Exception) {
        }
        try {
            float y = obj["y"].AsFloat * POSITION_FROM_WIRE_SCALE_FACTOR;
            if (_position.y != y) {
                _position.y = y;
                somethingChanged = true;
            }
        } catch (Exception) {
        }
        try {
            float z = obj["z"].AsFloat * POSITION_FROM_WIRE_SCALE_FACTOR;
            if (_position.z != z) {
                _position.z = z;
                somethingChanged = true;
            }
        } catch (Exception) {
        }
        const float ORIENTATION_FROM_WIRE_SCALE_FACTOR = 1.0f / 1000.0f;
        try {
            float W = obj["W"].AsFloat * ORIENTATION_FROM_WIRE_SCALE_FACTOR;
            if (_orientation.w != W) {
                _orientation.w = W;
                somethingChanged = true;
            }
        } catch (Exception) {
        }
        try {
            float X = obj["X"].AsFloat * ORIENTATION_FROM_WIRE_SCALE_FACTOR;
            if (_orientation.x != X) {
                _orientation.x = X;
                somethingChanged = true;
            }
        } catch (Exception) {
        }
        try {
            float Y = obj["Y"].AsFloat * ORIENTATION_FROM_WIRE_SCALE_FACTOR;
            if (_orientation.y != Y) {
                _orientation.y = Y;
                somethingChanged = true;
            }
        } catch (Exception) {
        }
        try {
            float Z = obj["Z"].AsFloat *  ORIENTATION_FROM_WIRE_SCALE_FACTOR;
            if (_orientation.z != Z) {
                _orientation.z = Z;
                somethingChanged = true;
            }
        } catch (Exception) {
        }
        return somethingChanged;
    }

    // for debug purposes
    public string ToWireFormattedJsonString() {
        // the key mappings are:
        //   J = _providedUserID
        //   e = _hashedVisitID
        //   v = _volumeDecibels
        //   x = Position.x
        //   y = Position.y
        //   z = Position.z
        //   W = Orientation.W
        //   X = Orientation.X
        //   Y = Orientation.y
        //   Z = Orientation.z
        JSONNode obj = new JSONObject();
        obj["J"] = _providedUserID;
        obj["e"] = _hashedVisitID;
        obj["v"] = _volumeDecibels;
        obj["x"] = _position.x;
        obj["y"] = _position.y;
        obj["z"] = _position.z;
        obj["W"] = _orientation.w;
        obj["X"] = _orientation.x;
        obj["Y"] = _orientation.y;
        obj["Z"] = _orientation.z;
        return obj.ToString();
    }
}

} // namespace HiFi
