using AlbumCoverMatchGame.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Store;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AlbumCoverMatchGame
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        public LicenseInformation AppLicenseInformation { get; set; }

        private ObservableCollection<Song> Songs;

        private ObservableCollection<StorageFile> AllSonds;

        private bool _playingMusic = false;

        private int _round = 0;

        private int _totalScore = 0;

        public MainPage()
        {
            
            this.InitializeComponent();
            Songs = new ObservableCollection<Song>();
        }
        
        private async Task RetrieveFilesInFolders(ObservableCollection<StorageFile> list,StorageFolder parent)
        {
            foreach (var item in await parent.GetFilesAsync())
            {
                if (item.FileType == ".mp3")
                {
                    list.Add(item);
                }
            }
            foreach (var item in await parent.GetFoldersAsync())
            {
                await RetrieveFilesInFolders(list, item);
            }
        }

        private async Task<List<StorageFile>> PickRandomSonds(ObservableCollection<StorageFile> allSonds)
        {
            Random random = new Random();
            var songCount = allSonds.Count();
            var randomSongs = new List<StorageFile>();
            while (randomSongs.Count < 10)
            {
                var randomNumber = random.Next(songCount);
                var randomSong = allSonds[randomNumber];
                MusicProperties randomSongMusicProperties = await randomSong.Properties.GetMusicPropertiesAsync();
                bool isDuplicate = false;
                foreach (var song in randomSongs)
                {
                    MusicProperties songMusicProperties = await song.Properties.GetMusicPropertiesAsync();
                    if (String.IsNullOrEmpty(randomSongMusicProperties.Album)||randomSongMusicProperties.Album==songMusicProperties.Album)
                        isDuplicate = true;
                }
                if (!isDuplicate)
                    randomSongs.Add(randomSong);
            }
            return randomSongs;
        }

        private async Task PopulateSongList(List<StorageFile> files)
        {
            int id = 0;

            foreach (var file in files)
            {
                MusicProperties songProperties = await file.Properties.GetMusicPropertiesAsync();

                StorageItemThumbnail currentThumb = await file.GetThumbnailAsync(ThumbnailMode.MusicView,200,ThumbnailOptions.UseCurrentScale);

                var albumCover = new BitmapImage();
                albumCover.SetSource(currentThumb);

                /*var song = new Song();
                song.Id = id;
                song.Title = songProperties.Title;
                song.Artist = songProperties.Artist;
                song.Album = songProperties.Album;
                song.AlbumCover = albumCover;
                song.SongFile = file;
                Songs.Add(song);*/

                var song = new Song()
                {
                    Id = id,
                    Title = songProperties.Title,
                    Artist = songProperties.Artist,
                    Album = songProperties.Album,
                    AlbumCover = albumCover,
                    SongFile = file
                };
                Songs.Add(song);
                id++;
            }
        }

        private async void SongGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (!_playingMusic) return;

            CountDown.Pause();
            MyMediaElement.Stop();

            var ClickedSong = (Song)e.ClickedItem;
            var correctSong = Songs.FirstOrDefault(p => p.Selected == true);
            Uri uri;
            int score;
            if (ClickedSong.Selected)
            {
                uri = new Uri("ms-appx:///Assets/correct.png");
                score = (int)MyProgressBar.Value;
            }
            else
            {
                uri = new Uri("ms-appx:///Assets/incorrect.png");
                score = (((int)MyProgressBar.Value)*-1)/2;
            }
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            var fileStream = await file.OpenAsync(FileAccessMode.Read);
            await ClickedSong.AlbumCover.SetSourceAsync(fileStream);
            _totalScore += score;
            ResultTextBlock.Text = string.Format("Score: {0} Total Score after {1} Rounds: {2}", score, _round, _totalScore);
            TitleTextBlock.Text = String.Format("Correct Song: {0}", correctSong.Title);
            ArtistTextBlock.Text = string.Format("Performed by: {0}", correctSong.Artist);
            AlbumTextBlock.Text = string.Format("On Album: {0}", correctSong.Album);
            ClickedSong.Used = true;
            correctSong.Selected = false;
            correctSong.Used = true;
            _round++;
            if (_round >= 5)
            {
                InstructionTextBlock.Text = string.Format("Game over ... You scored: {0}", _totalScore);
                PlayAgainButton.Visibility = Visibility.Visible;
            }
            else
            {
                StartCooldown();
            }
        }

        private async void PlayAgainButton_Click(object sender, RoutedEventArgs e)
        {
            await PrepareNewGame();
            PlayAgainButton.Visibility = Visibility.Collapsed;
        }

        private async Task<ObservableCollection<StorageFile>> SetupMusicList()
        {
            StorageFolder folder = KnownFolders.MusicLibrary;
            var allSonds = new ObservableCollection<StorageFile>();
            await RetrieveFilesInFolders(allSonds, folder);
            return allSonds;
        }

        private async Task PrepareNewGame()
        {
            Songs.Clear();
            var randomSongs = await PickRandomSonds(AllSonds);
            await PopulateSongList(randomSongs);
            MyMediaElement.Visibility = Visibility.Collapsed;
            SkipCooldown.Visibility = Visibility.Collapsed;
            StopCooldown.Visibility = Visibility.Collapsed;
            MyProgressBar.Visibility = Visibility.Collapsed;
            MyProgressBar.Visibility = Visibility.Visible;
            StartCooldown();
            InstructionTextBlock.Text = "Get ready ...";
            ResultTextBlock.Text = "";
            TitleTextBlock.Text = "";
            ArtistTextBlock.Text = "";
            AlbumTextBlock.Text = "";
            _totalScore = 0;
            _round = 0;
        }

        private async void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            StartupProgressRing.IsActive = true;
            AllSonds = await SetupMusicList();
            await PrepareNewGame();
            StartupProgressRing.IsActive = false;
            StartCooldown();
        }

        private void StartCooldown()
        {
            MyMediaElement.Visibility = Visibility.Visible;
            SkipCooldown.Visibility = Visibility.Visible;
            StopCooldown.Visibility = Visibility.Visible;
            _playingMusic = false;
            SolidColorBrush brush = new SolidColorBrush(Colors.Blue);
            MyProgressBar.Foreground = brush;
            InstructionTextBlock.Text = string.Format("Get ready for round {0} ...", _round + 1);
            InstructionTextBlock.Foreground = brush;
            CountDown.Begin();
        }

        private async void SkipCooldown_ClickAsync(object sender, RoutedEventArgs e)
        {
            if (!_playingMusic)
            {
                var song = PickSong();
                MyMediaElement.SetSource(await song.SongFile.OpenAsync(FileAccessMode.Read), song.SongFile.ContentType);
                StartCountdown();
            }
        }

        private void StopCooldown_Click(object sender, RoutedEventArgs e)
        {
            CountDown.Stop();
            StopCooldown.Visibility = Visibility.Collapsed;
        }

        private void StartCountdown()
        {
            SkipCooldown.Visibility = Visibility.Collapsed;
            StopCooldown.Visibility = Visibility.Collapsed;
            _playingMusic = true;
            SolidColorBrush brush = new SolidColorBrush(Colors.Red);
            MyProgressBar.Foreground = brush;
            InstructionTextBlock.Text = "GO!";
            InstructionTextBlock.Foreground = brush;
            CountDown.Begin();
        }

        private async void CountDown_Completed(object sender, object e)
        {
            if (!_playingMusic)
            {
                var song = PickSong();
                MyMediaElement.SetSource(await song.SongFile.OpenAsync(FileAccessMode.Read), song.SongFile.ContentType);
                StartCountdown();
            }
        }

        private Song PickSong()
        {
            Random random = new Random();
            var unusedSongs = Songs.Where(p => p.Used == false);
            var randomNumber = random.Next(unusedSongs.Count());
            var randomSong = unusedSongs.ElementAt(randomNumber);
            randomSong.Selected = true;
            return randomSong;
        }

        
    }
}
