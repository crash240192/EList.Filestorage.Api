namespace EList.Filestorage.BackgroundUploader
{
    public interface IBackgroundUploaderService
    {
        public bool Active { get; }
        
        void ManualStart();
        void ManualStop();

        void Start();
        void SendToXds();
        void Stop();
    }
}