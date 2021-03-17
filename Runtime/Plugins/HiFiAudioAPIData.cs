// HiFiAudioAPIData.cs

using SimpleJSON;
using UnityEngine;

namespace HiFi {

[System.Serializable]
public class AudioAPIDataChanges {
    public float? x; // pos.x
    public float? y; // pos.y
    public float? z; // pos.z
    public float? W; // Q.w
    public float? X; // Q.x
    public float? Y; // Q.y
    public float? Z; // Q.z
    public float? T; // noiseThreshold
    public float? g; // gain
    public float? a; // attenutation
    public float? r; // rolloff

    public bool IsNull() {
        return x == null && y == null && z == null
            && W == null && X == null && Y == null && Z == null
            && T == null && g == null && a == null && r == null;
    }

    public string ToJsonString() {
        JSONNode obj = new JSONObject();
        if (x.HasValue) {
            obj["x"] = x.Value;
        }
        if (y.HasValue) {
            obj["y"] = y.Value;
        }
        if (z.HasValue) {
            obj["z"] = z.Value;
        }
        if (W.HasValue) {
            obj["W"] = W.Value;
        }
        if (X.HasValue) {
            obj["X"] = X.Value;
        }
        if (Y.HasValue) {
            obj["Y"] = Y.Value;
        }
        if (Z.HasValue) {
            obj["Z"] = Z.Value;
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
    // NOTE: all data members are nullable types.
    /**
     * ✔ The client sends `position` data to the server when `_transmitHiFiAudioAPIDataToServer()` is called.
     *
     * ✔ The server sends `position` data to all clients connected to a server during "peer updates".
     */
    public Vector3? _position;
    /**
     * ✔ The client sends `orientation` data to the server when `_transmitHiFiAudioAPIDataToServer()` is called.
     *
     * ✔ The server sends `orientation` data to all clients connected to a server during "peer updates".
     */
    public Quaternion? _orientation;
    //Vector3 orientationEuler; // unsupported for Unity
    /**
     * A volume level below this value is considered background noise and will be smoothly gated off.
     * The floating point value is specified in dBFS (decibels relative to full scale) with values between -96 dB (indicating no gating)
     * and 0 dB. It is in the same decibel units as the VolumeDecibels component of UserDataSubscription.
     * If you don't supply a `volueThreshold` when constructing instantiations of this class `volumeThreshold` will be `null`.
     */
    public float? _volumeThreshold;
    /**
     * This value affects how loud UserA will sound to UserB at a given distance in 3D space.
     * This value also affects the distance at which UserA can be heard in 3D space.
     * Higher values for UserA means that UserA will sound louder to other users nearby, and it also means that UserA will be audible from a greater distance.
     * If you don't supply an `hiFiGain` when constructing instantiations of this class, `hiFiGain` will be `null`.
     */
    public float? _hiFiGain;
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
     * If you don't supply an `userAttenuation` when constructing instantiations of this class, `userAttenuation` will be `null` and the
     * default will be used.
     *
     * ✔ The client sends `userAttenuation` data to the server when `_transmitHiFiAudioAPIDataToServer()` is called.
     *
     * ❌ The server never sends `userAttenuation` data.
     */
    public float? _userAttenuation;
    /**
     * @param userRolloff This value represents the progressive high frequency roll-off in meters, a measure of how the higher frequencies
     * in a user's sound are dampened as the user gets further away. By default, there is a global roll-off value (set for a given space), currently 16
     * meters, which applies to all users in a space. This value represents the distance for a 1kHz rolloff. Values in the range of
     * 12 to 32 meters provide a more "enclosed" sound, in which high frequencies tend to be dampened over distance as they are
     * in the real world. Generally changes to roll-off values should be made for the entire space rather than for individual users, but
     * extremely high values (e.g. 99999) should be used in combination with "broadcast mode"-style userAttenuation settings to cause the
     * broadcasted voice to sound crisp and "up close" even at very large distances.
     *
     * If you don't supply an `userRolloff` when constructing instantiations of this class, `userRolloff` will be `null`.
     *
     * ✔ The client sends `userRolloff` data to the server when `_transmitHiFiAudioAPIDataToServer()` is called.
     *
     * ❌ The server never sends `userRolloff` data.
     */
    public float? _userRolloff;

    public AudioAPIDataChanges ApplyAndGetChanges(OutgoingAudioAPIData other) {
        AudioAPIDataChanges changes = new AudioAPIDataChanges();

        if (other == null) {
            return changes;
        }
        // Remember: all data members are nullable types!

        if (other._position.HasValue) {
            if (!_position.HasValue) {
                Vector3 otherPos = other._position.Value;
                _position = otherPos;
                changes.x = otherPos.x;
                changes.y = otherPos.y;
                changes.z = otherPos.z;
            } else if (_position != other._position) {
                Vector3 pos = _position.Value;
                Vector3 otherPos = other._position.Value;
                if (pos.x != otherPos.x) {
                    changes.x = otherPos.x;
                }
                if (pos.y != otherPos.y) {
                    changes.y = otherPos.y;
                }
                if (pos.z != otherPos.z) {
                    changes.z = otherPos.z;
                }
                _position = otherPos;
            }
        }

        if (other._orientation.HasValue) {
            if (!_orientation.HasValue) {
                Quaternion otherOrientation = other._orientation.Value;
                _orientation = otherOrientation;
                changes.W = otherOrientation.w;
                changes.X = otherOrientation.x;
                changes.Y = otherOrientation.y;
                changes.Z = otherOrientation.z;
            } else if (_orientation != other._orientation) {
                Quaternion orientation = _orientation.Value;
                Quaternion otherOrientation = other._orientation.Value;
                if (orientation.w != otherOrientation.w) {
                    changes.W = otherOrientation.w;
                }
                if (orientation.x != otherOrientation.x) {
                    changes.X = otherOrientation.x;
                }
                if (orientation.y != otherOrientation.y) {
                    changes.Y = otherOrientation.y;
                }
                if (orientation.z != otherOrientation.z) {
                    changes.Z = otherOrientation.z;
                }
                _orientation = otherOrientation;
            }
        }


        if (other._volumeThreshold.HasValue) {
            if (!_volumeThreshold.HasValue || _volumeThreshold != other._volumeThreshold) {
                _volumeThreshold = other._volumeThreshold.Value;
                changes.T = _volumeThreshold.Value;
            }
        }

        if (other._hiFiGain.HasValue) {
            if (!_hiFiGain.HasValue || _hiFiGain != other._hiFiGain) {
                _hiFiGain = other._hiFiGain.Value;
                changes.g = _hiFiGain.Value;
            }
        }

        if (other._userAttenuation.HasValue) {
            if (!_userAttenuation.HasValue || _userAttenuation != other._userAttenuation) {
                _userAttenuation = other._userAttenuation.Value;
                changes.a = _userAttenuation.Value;
            }
        }

        if (other._userRolloff.HasValue) {
            if (!_userRolloff.HasValue || _userRolloff != other._userRolloff) {
                _userRolloff = other._userRolloff.Value;
                changes.r = _userRolloff.Value;
            }
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
    public float? _volumeDecibels;

/*
    public IncomingAudioAPIData() : base() {
        // all fields are initialized to null in the ctor
        _providedUserID = null;
        _hashedVisitID = null;
        _volumeDecibels = null;
        // user must selectively set fields non-null after ctor
    }
*/
}

} // namespace HiFi
