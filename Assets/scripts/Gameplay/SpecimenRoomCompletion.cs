using UnityEngine;

[DefaultExecutionOrder(-80)]
[DisallowMultipleComponent]
public class SpecimenRoomCompletion : MonoBehaviour
{
    [SerializeField] private string cabinetDisposedSignal = "cabinet_jar_disposed";
    [SerializeField] private string tableDisposedSignal = "table_jar_disposed";
    [SerializeField] private string completedSignal = "specimen_room_complete";

    public bool Completed { get; private set; }

    private GameplaySignals subscribedSignals;

    private void OnEnable()
    {
        SubscribeAndCheck();
    }

    private void Start()
    {
        SubscribeAndCheck();
    }

    private void SubscribeAndCheck()
    {
        GameplaySignals signals = GameplaySignals.Instance;
        if (signals == null) return;

        if (subscribedSignals != signals)
        {
            if (subscribedSignals != null)
                subscribedSignals.SignalRaised -= OnSignalRaised;

            subscribedSignals = signals;
            subscribedSignals.SignalRaised += OnSignalRaised;
        }

        CheckCompletion();
    }

    private void OnSignalRaised(string signalId)
    {
        if (signalId == cabinetDisposedSignal || signalId == tableDisposedSignal)
            CheckCompletion();
    }

    private void CheckCompletion()
    {
        if (Completed || subscribedSignals == null) return;
        if (!subscribedSignals.HasRaised(cabinetDisposedSignal) ||
            !subscribedSignals.HasRaised(tableDisposedSignal)) return;

        Completed = true;
        subscribedSignals.Raise(completedSignal);
    }

    private void OnDisable()
    {
        if (subscribedSignals != null)
            subscribedSignals.SignalRaised -= OnSignalRaised;
        subscribedSignals = null;
    }
}
