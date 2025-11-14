using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityUtils;

public class SessionManager : Singleton<SessionManager>
{
    ISession activeSession;

    public ISession ActiveSession
    {
        get => activeSession;
        private set
        {
            activeSession = value;
            Debug.Log($"Active session: {activeSession}");
        }
    }

    const string playerNamePropertyKey = "playerName";

    async void Start()
    {
        try
        {
            // Only initialize and sign in — no automatic session creation
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"Signed in. PlayerID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

  

    public async UniTask CreateSessionAsHost()
    {
        try
        {
            var playerProperties = await GetPlayerProperties();

            var options = new SessionOptions
            {
                MaxPlayers = 2,
                IsLocked = false,
                IsPrivate = false,
                PlayerProperties = playerProperties
            }.WithRelayNetwork();

            ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            Debug.Log($"Session created. ID: {ActiveSession.Id}, Join Code: {ActiveSession.Code}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create session: {e}");
        }
    }

    public async UniTask JoinSessionByCode(string sessionCode)
    {
        try
        {
            ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode);
            Debug.Log($"Joined session by code. ID: {ActiveSession.Id}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join session by code: {e}");
        }
    }

    public async UniTask JoinSessionById(string sessionId)
    {
        try
        {
            ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
            Debug.Log($"Joined session by ID: {ActiveSession.Id}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join session by ID: {e}");
        }
    }

    public async UniTask LeaveSession()
    {
        if (ActiveSession == null)
        {
            Debug.LogWarning("No active session to leave.");
            return;
        }

        try
        {
            await ActiveSession.LeaveAsync();
            Debug.Log("Left session successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error leaving session: {e}");
        }
        finally
        {
            ActiveSession = null;
        }
    }

    public async UniTask KickPlayer(string playerId)
    {
        if (ActiveSession == null || !ActiveSession.IsHost)
        {
            Debug.LogWarning("You must be host to kick players.");
            return;
        }

        try
        {
            await ActiveSession.AsHost().RemovePlayerAsync(playerId);
            Debug.Log($"Player {playerId} was kicked.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to kick player: {e}");
        }
    }

    public async UniTask<IList<ISessionInfo>> QuerySessions()
    {
        var sessionQueryOptions = new QuerySessionsOptions();
        QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(sessionQueryOptions);
        return results.Sessions;
    }

    async UniTask<Dictionary<string, PlayerProperty>> GetPlayerProperties()
    {
        var playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
        var playerNameProperty = new PlayerProperty(playerName, VisibilityPropertyOptions.Member);
        return new Dictionary<string, PlayerProperty> { { playerNamePropertyKey, playerNameProperty } };
    }
}
