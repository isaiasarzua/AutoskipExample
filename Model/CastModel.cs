using System.Collections.ObjectModel;
using System.Diagnostics;
using LibVLCSharp.Shared;

namespace AutoskipExample
{
    /// <summary>
    /// Handles cast logic.
    /// </summary>
    public class CastModel
    {
        public CastModel() { rendererItems = new ObservableCollection<RendererItem>(); }

        RendererDiscoverer _rendererDiscoverer;
        public ObservableCollection<RendererItem> rendererItems { get; set; }


        public void DiscoverRenderers(LibVLC _libVLC)
        {
            Debug.WriteLine("Searching for renderers");
            // create a renderer discoverer
            _rendererDiscoverer = new RendererDiscoverer(_libVLC);

            // register callback when a new renderer is found
            _rendererDiscoverer.ItemAdded += RendererDiscoverer_ItemAdded;

            // start discovery on the local network
            _rendererDiscoverer.Start();
        }

        void RendererDiscoverer_ItemAdded(object sender, RendererDiscovererItemAddedEventArgs e)
        {
            Debug.WriteLine($"New item discovered: {e.RendererItem.Name} of type {e.RendererItem.Type}");

            if (e.RendererItem.CanRenderVideo)
                Debug.WriteLine("Can render video");
            if (e.RendererItem.CanRenderAudio)
                Debug.WriteLine("Can render audio");

            // delegating to UI thread
            App.Current.Dispatcher.Invoke(delegate
            {
                // add newly found renderer item to local collection 
                rendererItems.Add(e.RendererItem);
            });
        }
    }
}