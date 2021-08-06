// HiFiConnectionAndTimeoutConfig.cs
//

using UnityEngine;

namespace HiFi {

public class HiFiConnectionAndTimeoutConfig {
    public const string DEFAULT_LOCAL_TEST_SIGNALING_URL = "ws://localhost:8889/";
    public const uint DEFAULT_RETRY_CONNECTION_TIMEOUT = 15; // sec
    public const uint DEFAULT_RECONNECTION_TIMEOUT = 60; // sec
    public const uint DEFAULT_CONNECTION_DELAY_MS = 500; // msec
    public const uint DEFAULT_CONNECTION_TIMEOUT_MS = 5000; // msec

    // Pay attention to the variable names we are using here:
    // (1) "retryConnection" applies to attempts to make the initial connection
    // (2) "reconnect" applies to attempts to make subsequent connections
    // (3) just "connection" applies to both

    // Whether or not to automatically retry initial connection attempts.
    // When this is set to true: if the first attempt to connect to the High
    // Fidelity servers fails we will automatically retry the connection.
    // While the connection is being attempted Communicator state will be
    // "Connecting". After the desired amount of time has passed, if a
    // connection has not been established we will stop trying and the state
    // will change to 'Failed'.  By default, connections are not retried.
    // However, when we start a connection: we do always trigger a state
    // change to Connecting when we start the connection process, regardless
    // of the setting of this value.
    //
    [Tooltip("Retry connection on failure")]
    public bool AutoRetryConnection = false;

    // The total amount of time (in seconds) to keep retrying the initial
    // connection before giving up completely. This defaults to 15 seconds.
    //
    [Tooltip("Seconds allowed for connection retries")]
    public uint RetryConnectionTimeout = DEFAULT_RETRY_CONNECTION_TIMEOUT; // sec

    // Whether or not to automatically attempt to reconnect if an existing
    // connection is disconnected. When this is set to true: we will attempt
    // to reconnect if any disconnect from any cause occurs.  By default,
    // reconnections are not automatically attempted.
    // NOTE: The reconnection attempts that occur when `AutoReconnect` is `true`
    // do not know or care as to WHY a connection was lost.  This means: if
    // the client lost connection because the server decided to end it (e.g.
    // a "kick") the client will still automatically attempt to reconnect.
    // Meanwhile, connections that are explicitly closed from the client side
    // via the `Communicator::disconnect()` method will transition to the
    // "Closing" state and then finally to "Closed".
    //
    [Tooltip("Reconnect on failure")]
    public bool AutoReconnect = false;

    // The total amount of time (in seconds) to keep attempting to reconnect
    // if an existing connection is disconnected and `AutoReconnect` is `true`.
    // While the connection is being attempted, the state will be
    // "Reconnecting".  After this amount of time has passed, if a connection
    // has not been stablished, we will stop trying and set the connection
    // state to 'Failed'.  This defaults to 60 seconds (1 minute).
    //
    [Tooltip("Seconds allowed for reconnection attempts")]
    public uint ReconnectionTimeout = DEFAULT_RECONNECTION_TIMEOUT; // sec

    // The amount of time in milliseconds to wait after a connection failure
    // before the next retry/reconnection attempt. This defaults to 500
    // milliseconds and can be used to slow down the connection attempts if
    // needed (e.g. for testing) You probably won't need to set this value.
    //
    [Tooltip("Msec delay between connection failure and next attempt")]
    public uint ConnectionDelayMs = DEFAULT_CONNECTION_DELAY_MS; // msec

    // The amount of time in milliseconds to wait before timing out an attempted
    // connection. This is used for all connection attempts. Defaults to 5000
    // milliseconds (5 seconds).
    //
    [Tooltip("Msec allowed for each connection attempt")]
    public uint ConnectionTimeoutMs = DEFAULT_CONNECTION_TIMEOUT_MS; // msec
};


} // namespace
