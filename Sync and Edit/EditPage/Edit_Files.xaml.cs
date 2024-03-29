﻿using SQLite;
using Sync_and_Edit.DataBase;
using Sync_and_Edit.Json;
using Sync_and_Edit.Menu;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace Sync_and_Edit.EditPage
{
    public sealed partial class Edit_Files : Page
    {
        Json_options Json = new Json_options();
        static string convert_type;
        static string audioteca;
        static string text_audioteca;
        static Song current_song;
        List<string> DeleteFolder = new List<string>(); 
        List<string> SongWithoutTag = new List<string>(); 
        List<Song> SongDeleteAfterConvert = new List<Song>();
        static int all_convert_song = 0, current_convert_song = 0;
        DatabaseHelperClass Db_Helper = new DatabaseHelperClass();
        StorageFolder Main_folder;

        public Edit_Files()
        {
            this.Read_Json();
            this.InitializeComponent();
            Edit_Page.my_list_box.SelectedIndex = 2;
        }

        private async void Read_Json()
        {
            Json = await Json.ReadJson();
            audioteca = Json.Json_audioteca;
            text_audioteca = Json.Json_audioteca.Split('\\').Last();
        }

        private async void Name_Click(object sender, RoutedEventArgs e)
        {
            using (SQLiteConnection db = new SQLiteConnection(App.DB_PATH))
            {
                if (Rename.SelectedValue != null)
                {
                    progressRing_name.IsActive = true;
                    var Tags = db.Query<Tag>("Select * from Tag");
                    if (Tags.Count() == 0)
                    {
                        var dialog = new MessageDialog("База пуста");
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        for (int i = 0; i < Tags.Count(); i++)
                        {
                            await Rename_function(db, Tags, i);
                        }
                        if (SongWithoutTag.Count != 0)
                        {
                            string temp = "";
                            foreach (string file in SongWithoutTag)
                            {
                                temp = temp + "\n - " + file;
                            }
                            progressRing_name.IsActive = false;
                            var dialog = new MessageDialog("Файлы переименованы, кроме песен " +
                                "приведенных ниже, так как у них нет тегов: " + temp);
                            await dialog.ShowAsync();
                            SongWithoutTag.Clear();
                        }
                        else
                        {
                            var dialog = new MessageDialog("Файлы переименованы");
                            await dialog.ShowAsync();
                        }
                    }
                }
                else
                {
                    var dialog = new MessageDialog("Не выбрано значение для перемещения");
                    await dialog.ShowAsync();
                }
            }
        }

        // Метод для переименования
        private async Task Rename_function(SQLiteConnection db, List<Tag> Tags, int i)
        {
            var temp_tag = Tags[i];
            string temp_name = "";
            if (Rename.SelectedIndex == 0)
            {
                temp_name = temp_tag.Artist + " - " + temp_tag.NameSong;
            }
            if (Rename.SelectedIndex == 1)
            {
                temp_name = temp_tag.NameSong;
            }
            var temp_song = db.Find<Song>(c => c.TagId == temp_tag.Id && c.NameSong != temp_name);
            if (temp_song != null)
            {
                if (temp_tag.Artist == "" || temp_tag.NameSong == "")
                {
                    temp_name = temp_song.NameSong;
                    SongWithoutTag.Add(temp_name);
                }
                var temp_format = db.Find<MusicFormat>(c => c.Id == temp_song.FormatId);
                var old_name = audioteca + "\\" + temp_song.Path + temp_song.NameSong + "." + temp_format.NameFormat;

                //*сделать регулярное выражение
                temp_name = temp_name.Replace(@"\", "").Replace(@"/", "").Replace(":", "").Replace("*", "").
                    Replace("?", "").Replace("<", "").Replace(">", "");
                var new_name = audioteca + "\\" + temp_song.Path + temp_name + "." + temp_format.NameFormat;

                await Task.Run(() =>
                {
                    //*убрать запрещенные знаки при переименовании
                    File.Move(old_name, new_name);
                    temp_song.NameSong = temp_name;
                    Db_Helper.Update_Song(temp_song);
                });
            }
        }
        private async Task Replace_function(SQLiteConnection db, List<Song> Songs, int i)
        {
            var temp_song = Songs[i];
            string temp_path = temp_song.Path;
            if (Replace.SelectedIndex == 0)
            {
                Main_folder = await Windows.Storage.AccessCache.StorageApplicationPermissions
                .FutureAccessList.GetFolderAsync("Audio");
                var temp_tag = db.Find<Tag>(c => c.Id == temp_song.TagId);

                try
                {
                    StorageFolder y = await Main_folder.CreateFolderAsync(temp_tag.Artist);
                }
                catch { }

                var temp_format = db.Find<MusicFormat>(c => c.Id == temp_song.FormatId);
                var old_name = audioteca + "\\" + temp_song.Path + temp_song.NameSong + "." + temp_format.NameFormat;
                var new_name = audioteca + "\\" + temp_tag.Artist + "\\" + temp_song.NameSong + "." + temp_format.NameFormat;

                if (temp_song.Path != "") //если изначальная папка была не аудиотека
                {
                    var folder = audioteca + "\\" + temp_song.Path.Replace("\\", "");
                    DeleteFolder.Add(folder);
                }

                await Task.Run(() =>
                {
                    File.Move(old_name, new_name);
                    if (temp_tag.Artist != "")
                    {
                        temp_song.Path = temp_tag.Artist + "\\";
                    }
                    Db_Helper.Update_Song(temp_song);
                });
            }
            if (Replace.SelectedIndex == 1)
            {
                if (temp_song.Path != "")
                {
                    var temp_format = db.Find<MusicFormat>(c => c.Id == temp_song.FormatId);
                    var old_name = audioteca + "\\" + temp_song.Path + temp_song.NameSong + "." + temp_format.NameFormat;
                    var new_name = audioteca + "" + "\\" + temp_song.NameSong + "." + temp_format.NameFormat;

                    var folder = audioteca + "\\" + temp_song.Path.Replace("\\", "");
                    DeleteFolder.Add(folder);

                    await Task.Run(() =>
                    {
                        File.Move(old_name, new_name);
                        temp_song.Path = "";
                        Db_Helper.Update_Song(temp_song);
                    });
                }
            }
        }

        private async void DirectoryClick(object sender, RoutedEventArgs e)
        {
            using (SQLiteConnection db = new SQLiteConnection(App.DB_PATH))
            {
                if (Replace.SelectedValue != null)
                {
                    progressRing_directory.IsActive = true;
                    var Songs = db.Query<Song>("Select * from Song");
                    if (Songs.Count() == 0)
                    {
                        var dialog = new MessageDialog("База пуста");
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        for (int i = 0; i < Songs.Count(); i++)
                        {
                            await Replace_function(db, Songs, i);
                        }
                        //Удаляем папки из списка полученных
                        DeleteFolder = DeleteFolder.Distinct().ToList<string>();
                        bool exist = false;
                        foreach (string folder in DeleteFolder)
                        {
                            await Task.Run(() =>
                            {
                                exist = Directory.Exists(folder);
                                if (exist != false && folder != audioteca + "\\")
                                //добавил аудиотеку, так как не все файлы имеют папку
                                {
                                    Directory.Delete(folder);
                                }
                            });
                        }
                        progressRing_directory.IsActive = false;
                        var dialog = new MessageDialog("Файлы перемещены");
                        await dialog.ShowAsync();
                    }
                }
                else
                {
                    var dialog = new MessageDialog("Не выбрано значение для перемещения");
                    await dialog.ShowAsync();
                }
            }
        }

        private async void Delete_oldfile()
        {
            using (SQLiteConnection db = new SQLiteConnection(App.DB_PATH))
            {
                foreach (var item in SongDeleteAfterConvert)
                {
                    var format = db.Find<MusicFormat>(c => c.Id == item.FormatId);
                    var old_file = audioteca + "\\" + item.NameSong + "." + format.NameFormat;
                    await Task.Run(() =>
                    {
                        File.Delete(old_file); //удаляем старый файл
                    });

                    var new_format = db.Find<MusicFormat>(c => c.NameFormat == convert_type);
                    item.FormatId = new_format.Id;

                    var import_name = item.Path + item.NameSong + "." + new_format.NameFormat;
                    var folder = await Windows.Storage.AccessCache.StorageApplicationPermissions
                    .FutureAccessList.GetFolderAsync("Audio");
                    StorageFile music_files = await folder.GetFileAsync(import_name);
                    MusicProperties musicProperties = await music_files.Properties.GetMusicPropertiesAsync();

                    StringBuilder fileProperties = new StringBuilder(); //размер файла в байтах
                    BasicProperties basicProperties = await music_files.GetBasicPropertiesAsync();
                    var new_size = Convert.ToInt32(basicProperties.Size);

                    await Task.Run(() =>
                    {
                        item.Bitrait = Convert.ToInt32(musicProperties.Bitrate);
                        item.Size = new_size;
                        Db_Helper.Update_Song(item);
                    });
                }
                progressRing_format.IsActive = false;
                var dialog = new MessageDialog("Конвертирование завершено");
                await dialog.ShowAsync();
            }
        }

        static AudioEncodingQuality Load_options(string format)
        {
            AudioEncodingQuality Quality;
            switch (format)
            {
                case "High":
                    Quality = AudioEncodingQuality.High;
                    break;
                case "Low":
                    Quality = AudioEncodingQuality.Low;
                    break;
                case "Medium":
                    Quality = AudioEncodingQuality.Medium;
                    break;
                default:
                    Quality = AudioEncodingQuality.High;
                    break;
            }
            return Quality;
        }

        private async Task Covert_music(string audio, string type)
        {
            using (SQLiteConnection db = new SQLiteConnection(App.DB_PATH))
            {
                MediaEncodingProfile profile = new MediaEncodingProfile();
                if (type == ".mp3")
                {
                    var Quality = Load_options(Json.Json_mp3);
                    profile = MediaEncodingProfile.CreateMp3(Quality);
                }
                else if (type == ".alac")
                {
                    var Quality = Load_options(Json.Json_alac);
                    profile = MediaEncodingProfile.CreateAlac(Quality);
                    type = ".m4a";
                }
                else if (type == ".flac")
                {
                    var Quality = Load_options(Json.Json_flac);
                    profile = MediaEncodingProfile.CreateFlac(Quality);
                }
                else if (type == ".aac")
                {
                    var Quality = Load_options(Json.Json_aac);
                    profile = MediaEncodingProfile.CreateM4a(Quality);
                    type = ".m4a";
                }
                convert_type = type.Substring(1);
                var songs = db.Query<Song>("Select * from Song");
                foreach (var song in songs)
                {
                    var format = db.Find<MusicFormat>(c => c.Id == song.FormatId);
                    var import_name = song.Path + song.NameSong + "." + format.NameFormat;
                    string new_name = song.Path + song.NameSong + type;

                    Main_folder = await Windows.Storage.AccessCache.StorageApplicationPermissions
                    .FutureAccessList.GetFolderAsync("Audio");
                    StorageFile oldfile = await Main_folder.GetFileAsync(import_name);
                    MusicProperties musicProperties = await oldfile.Properties.GetMusicPropertiesAsync();
                    if (oldfile.FileType != ".mp3" && oldfile.FileType != type &&
                        musicProperties.Bitrate > 500000)
                    {
                        StorageFile newfile = await Main_folder.CreateFileAsync(new_name, CreationCollisionOption.ReplaceExisting);
                        if (oldfile != null && newfile != null)
                        {
                            try
                            {
                                current_song = song;
                                all_convert_song++;
                                SongDeleteAfterConvert.Add(current_song);
                                MediaTranscoder transcoder = new MediaTranscoder();
                                PrepareTranscodeResult prepareOp = await transcoder.PrepareFileTranscodeAsync(oldfile, newfile, profile);
                                if (prepareOp.CanTranscode)
                                {
                                    var transcodeOp = prepareOp.TranscodeAsync();
                                    transcodeOp.Completed += async (IAsyncActionWithProgress<double> asyncInfo, AsyncStatus status) =>
                                    {
                                        asyncInfo.GetResults();
                                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                        {

                                            current_convert_song++;
                                        });
                                    };
                                }
                            }
                            catch (Exception exc)
                            {
                                var dialog = new MessageDialog(exc.ToString());
                                await dialog.ShowAsync();
                            }
                        }
                    }
                }
                do
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));
                } 
                while (all_convert_song != current_convert_song);
                Delete_oldfile();
            }
        }

        private async void Ask_dialog(string text, string type)
        {
            ContentDialog noWifiDialog = new ContentDialog
            {
                Title = "Конвертирование музыкальных файлов",
                Content = text,
                PrimaryButtonText = "Применить",
                SecondaryButtonText = "Отмена"
            };
            ContentDialogResult result = await noWifiDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                progressRing_format.IsActive = true;
                await Covert_music(audioteca, type);

            }
        }

        private async void FormatClick(object sender, RoutedEventArgs e)
        {
            using (SQLiteConnection db = new SQLiteConnection(App.DB_PATH))
            {
                if (Convert_combo.SelectedValue != null)
                {
                    var Songs = db.Query<Song>("Select * from Song");
                    if (Songs.Count() == 0)
                    {
                        var dialog = new MessageDialog("База пуста");
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        if (Convert_combo.SelectedIndex == 0)
                        {
                            Ask_dialog("Все музыкальные файлы без потерь будут переконвертированы в flac", ".flac");
                        }
                        if (Convert_combo.SelectedIndex == 1)
                        {
                            Ask_dialog("Все музыкальные файлы будут переконвертированы в mp3", ".mp3");
                        }
                        if (Convert_combo.SelectedIndex == 2)
                        {
                            Ask_dialog("Все музыкальные файлы без потерь будут переконвертированы в alac", ".alac");
                        }
                        if (Convert_combo.SelectedIndex == 3)
                        {
                            Ask_dialog("Все музыкальные файлы без потерь будут переконвертированы в aac", ".aac");
                        }
                        if (Convert_combo.SelectedIndex == 4)
                        {
                            Ask_dialog("Все музыкальные файлы без потерь будут переконвертированы в wav", ".wav");
                        }
                    }
                }
                else
                {
                    var dialog = new MessageDialog("Не выбрано значение для перекодирования");
                    await dialog.ShowAsync();
                }
            }
        }
    }
}
