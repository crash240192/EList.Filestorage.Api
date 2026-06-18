namespace EList.Filestorage.BackgroundWorker
{
    public interface IBackgroundWorkerService
    {
        public bool Active { get; }
        
        void ManualStart();
        void ManualStop();

        void Start();
        void Process();
        void Stop();
    }
}