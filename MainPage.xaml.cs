using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.PlayTo;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SalvarPlayTo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        #region - variáveis e objetos iniciais -

        // tipos de mídia transmitidas
        enum TipoMidia
        {
            Nenhum,
            Imagem,
            AudioVideo
        }
        TipoMidia tipoMidia = TipoMidia.Nenhum;

        // receptor do DLNA PlayTo
        PlayToReceiver receptor = null;
        bool receptorIniciou = false;

        // mídia player
        bool estaReposicionandoMidia = false;
        double taxaExecucaoMidia = 0;
        bool estaMidiaCarregada = false;
        bool IsPlayReceivedPreMediaLoaded = false;

        // visualizador de imagens
        BitmapImage imagemEnviada = null;

        // está em processo de salvar os dados vindos da transmissão?
        bool estaGravando = false;
        const int TAMANHO_BUFFER = 1024000;

        public MainPage()
        {
            this.InitializeComponent();
        }

        #endregion

        // escolher pasta de destino
        private async void EscolherPastaButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".");

            StorageFolder pasta = await picker.PickSingleFolderAsync();
            if (pasta != null)
            {
                Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("MM_SalvarPlayTo_meuTokenDaPasta", pasta);
                PastaDestinoTextBox.Text = pasta.Path;
                IniciarReceptorButton.IsEnabled = true;
            }

        }

        // botão liga/desliga da recepção
        private async void IniciarReceptorButton_Click(object sender, RoutedEventArgs e)
        {
            Button botao = sender as Button;
            if (botao != null)
            {
                if (receptor == null)
                {
                    try
                    {

                        // cria o receptor de DLNA
                        receptor = new PlayToReceiver();

                        receptor.PlayRequested += new TypedEventHandler<PlayToReceiver, object>(receptor_PlayRequested);
                        receptor.PauseRequested += new TypedEventHandler<PlayToReceiver, object>(receptor_PauseRequested);
                        receptor.StopRequested += new TypedEventHandler<PlayToReceiver, object>(receptor_StopRequested);
                        receptor.TimeUpdateRequested += new TypedEventHandler<PlayToReceiver, object>(receptor_TimeUpdateRequested);
                        receptor.CurrentTimeChangeRequested += new TypedEventHandler<PlayToReceiver, CurrentTimeChangeRequestedEventArgs>(receptor_CurrentTimeChangeRequested);
                        receptor.SourceChangeRequested += new TypedEventHandler<PlayToReceiver, SourceChangeRequestedEventArgs>(receptor_SourceChangeRequested);
                        receptor.MuteChangeRequested += new TypedEventHandler<PlayToReceiver, MuteChangeRequestedEventArgs>(receptor_MuteChangeRequested);
                        receptor.PlaybackRateChangeRequested += new TypedEventHandler<PlayToReceiver, PlaybackRateChangeRequestedEventArgs>(receptor_PlaybackRateChangeRequested);
                        receptor.VolumeChangeRequested += new TypedEventHandler<PlayToReceiver, VolumeChangeRequestedEventArgs>(receptor_VolumeChangeRequested);

                        receptor.SupportsAudio = true;
                        receptor.SupportsVideo = true;
                        receptor.SupportsImage = true;

                        receptor.FriendlyName = "MM SalvarPlayTo";

                        EventosTextBox.Text += $"{DateTime.Now} - iniciou o receptor\r\n";
                        await receptor.StartAsync();
                        receptorIniciou = true;
                        botao.Content = "Parar receptor";
                    }
                    catch (Exception ex)
                    {
                        receptorIniciou = false;
                        EventosTextBox.Text += $"{DateTime.Now} - erro ao iniciar o receptor\r\n";
                        EventosTextBox.Text += $"     {ex.Message}\r\n\r\n";
                    }
                }
                else
                {

                    // destrói o receptor
                    await receptor.StopAsync();

                    receptor.PlayRequested -= new TypedEventHandler<PlayToReceiver, object>(receptor_PlayRequested);
                    receptor.PauseRequested -= new TypedEventHandler<PlayToReceiver, object>(receptor_PauseRequested);
                    receptor.StopRequested -= new TypedEventHandler<PlayToReceiver, object>(receptor_StopRequested);
                    receptor.TimeUpdateRequested -= new TypedEventHandler<PlayToReceiver, object>(receptor_TimeUpdateRequested);
                    receptor.CurrentTimeChangeRequested -= new TypedEventHandler<PlayToReceiver, CurrentTimeChangeRequestedEventArgs>(receptor_CurrentTimeChangeRequested);
                    receptor.SourceChangeRequested -= new TypedEventHandler<PlayToReceiver, SourceChangeRequestedEventArgs>(receptor_SourceChangeRequested);
                    receptor.MuteChangeRequested -= new TypedEventHandler<PlayToReceiver, MuteChangeRequestedEventArgs>(receptor_MuteChangeRequested);
                    receptor.PlaybackRateChangeRequested -= new TypedEventHandler<PlayToReceiver, PlaybackRateChangeRequestedEventArgs>(receptor_PlaybackRateChangeRequested);
                    receptor.VolumeChangeRequested -= new TypedEventHandler<PlayToReceiver, VolumeChangeRequestedEventArgs>(receptor_VolumeChangeRequested);

                    receptor = null;

                    receptorIniciou = false;
                    botao.Content = "Iniciar receptor";
                    EventosTextBox.Text += $"{DateTime.Now} - parou o receptor\r\n";
                }
            }
        }

        #region - eventos vindos do transmissor para o receptor -

        // o transmissor requisitou que o receptor execute a mídia
        private async void receptor_PlayRequested(PlayToReceiver recv, Object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (tipoMidia == TipoMidia.AudioVideo)
                {
                    PlayerMediaElement.Play();
                    receptor.NotifyPlaying();
                }
                else if (tipoMidia == TipoMidia.Imagem)
                {
                    ImagemImage.Source = imagemEnviada;
                    receptor.NotifyPlaying();
                }
                EventosTextBox.Text += $"{DateTime.Now} - requisitada a execução da mídia\r\n";
            });
        }

        // o transmissor requisitou que o receptor pause a mídia
        private async void receptor_PauseRequested(PlayToReceiver recv, Object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (tipoMidia == TipoMidia.AudioVideo)
                {
                    if (PlayerMediaElement.CurrentState == MediaElementState.Stopped)
                    {
                        receptor.NotifyPaused();
                    }
                    else
                    {
                        PlayerMediaElement.Pause();
                        receptor.NotifyPaused();
                    }
                }
                EventosTextBox.Text += $"{DateTime.Now} - requisitada a pausa da mídia\r\n";
            });
        }

        // o transmissor requisitou que o receptor interrompa a mídia
        private async void receptor_StopRequested(PlayToReceiver recv, Object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (tipoMidia == TipoMidia.AudioVideo)
                {
                    PlayerMediaElement.Stop();
                    receptor.NotifyStopped();
                }
                else if (tipoMidia == TipoMidia.Imagem)
                {
                    ImagemImage.Source = null;
                    receptor.NotifyStopped();
                }
                EventosTextBox.Text += $"{DateTime.Now} - requisitada a interrupção da execução da mídia\r\n";
            });
        }

        // o transmissor requisitou que o receptor o informe sobre o posicionamento na mídia
        private async void receptor_TimeUpdateRequested(PlayToReceiver recv, Object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (receptorIniciou)
                {
                    if (tipoMidia == TipoMidia.AudioVideo)
                    {
                        receptor.NotifyTimeUpdate(PlayerMediaElement.Position);
                    }
                    else if (tipoMidia == TipoMidia.Imagem)
                    {
                        receptor.NotifyTimeUpdate(new TimeSpan(0));
                    }
                }
            });
        }

        // o transmissor requisitou que o receptor atualize seu posicionamento na mídia
        private async void receptor_CurrentTimeChangeRequested(PlayToReceiver recv, CurrentTimeChangeRequestedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (receptorIniciou)
                {
                    if (tipoMidia == TipoMidia.AudioVideo)
                    {
                        if (PlayerMediaElement.CanSeek)
                        {
                            PlayerMediaElement.Position = args.Time;
                            receptor.NotifySeeking();
                            estaReposicionandoMidia = true;
                        }
                    }
                    else if (tipoMidia == TipoMidia.Imagem)
                    {
                        receptor.NotifySeeking();
                        receptor.NotifySeeked();
                    }
                }
                EventosTextBox.Text += $"{DateTime.Now} - requisitado o reposicionamento na mídia\r\n";
            });
        }

        // o transmissor requisitou que o receptor altere a origem da mídia
        private async void receptor_SourceChangeRequested(PlayToReceiver recv, SourceChangeRequestedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
            {
                EventosTextBox.Text += $"{DateTime.Now} - requisitada mudança na origem da mídia\r\n";
                PlayerMediaElement.Source = null;
                ImagemImage.Source = null;

                // o transmissor pode não enviar nenhuma mídia
                if (args.Stream == null)
                {

                    EventosTextBox.Text += $"     * origem de mídia vazia!\r\n";
                    ImagemImage.Opacity = 0;
                    tipoMidia = TipoMidia.Nenhum;
                    estaGravando = false;

                }
                else
                {

                    // informações sobre a mídia sendo transmitida
                    string propriedadesArquivo = "  - Propriedades:\r\n";
                    string nomeArquivo = "";
                    ulong tamanhoArquivo = args.Stream.Size;

                    // propriedades do arquivo
                    foreach (KeyValuePair<string, object> item in args.Properties)
                    {
                        propriedadesArquivo += $"     {item.Key} = {item.Value.ToString()}\r\n";

                        // obtendo nome do arquivo
                        if (item.Key == "System.FileName") nomeArquivo = item.Value.ToString();
                    }

                    EventosTextBox.Text = $"Mídia transmitida:\r\n  - Título: {args.Title}\r\n  - Descrição: {args.Description}\r\n";

                    if (nomeArquivo == "")
                    {
                        EventosTextBox.Text += $"   * NÃO LOCALIZADO O NOME DO ARQUIVO\r\n";
                    }
                    else
                    {
                        EventosTextBox.Text += $"  - Nome do arquivo: {nomeArquivo}\r\n";
                    }

                    EventosTextBox.Text += $"  - Tamanho do arquivo: {FormatarTamanhoArquivo(tamanhoArquivo)} ({tamanhoArquivo} bytes)\r\n";
                    EventosTextBox.Text += propriedadesArquivo;

                    // auto salvar o arquivo
                    // BUG NO PLAYTO DO HTC ULTIMATE: ele dispara este evento duplicadamente!
                    // verificar se gravando para evitar o bug do HTC
                    bool gravarArquivo = false;
                    if (AutoSalvarCheckBox.IsChecked == true && !estaGravando && nomeArquivo != "" && tamanhoArquivo != 0)
                    {
                        estaGravando = true;
                        EventosTextBox.Text += $"{DateTime.Now}    * iniciando a gravação da mídia\r\n";

                        // verifica se o arquivo destino já existe
                        StorageFolder pastaDestino = await StorageFolder.GetFolderFromPathAsync(PastaDestinoTextBox.Text);
                        IStorageItem itemDestino = await pastaDestino.TryGetItemAsync(nomeArquivo);

                        // se não existe, gravar a mídia
                        if (itemDestino == null)
                        {
                            gravarArquivo = true;
                        }
                        else
                        {
                            if (itemDestino.IsOfType(StorageItemTypes.File))
                            {

                                // se já existe um arquivo com esse nome, verificar se os tamanhos são diferentes
                                StorageFile arquivoExistente = (StorageFile)itemDestino;
                                BasicProperties propriedades = await arquivoExistente.GetBasicPropertiesAsync();
                                if (propriedades.Size != tamanhoArquivo)
                                {
                                    EventosTextBox.Text += $"{DateTime.Now}    * já existe arquivo com tamanho diferente\r\n";
                                    gravarArquivo = true;
                                }
                                else
                                {
                                    EventosTextBox.Text += $"{DateTime.Now}    * já existe arquivo com mesmo tamanho\r\n";
                                }
                            }
                        }

                        // é para gravar a mídia?
                        if (gravarArquivo)
                        {

                            receptor.NotifyLoadedMetadata();
                            receptor.NotifyPlaying();

                            // lê os dados da mídia
                            using (var dadosMidia = args.Stream.GetInputStreamAt(0))
                            {

                                // cria o arquivo de destino
                                EventosTextBox.Text += $"{DateTime.Now}    * criando o arquivo destino\r\n";
                                StorageFile arquivoDestino = await pastaDestino.CreateFileAsync(nomeArquivo, CreationCollisionOption.GenerateUniqueName);
                                using (var dadosArquivo = await arquivoDestino.OpenAsync(FileAccessMode.ReadWrite))
                                {

                                    // acessa os dados do arquivo destino
                                    using (var dataWriter = new DataWriter(dadosArquivo.GetOutputStreamAt(0)))
                                    {

                                        EventosTextBox.Text += $"{DateTime.Now}    * iniciando o acesso aos dados da mídia\r\n";
                                        using (var dataReader = new DataReader(dadosMidia))
                                        {
                                            // laço lendo os dados da mídia, de acordo com o tamanho do buffer
                                            BarraProgresso.Maximum = tamanhoArquivo;
                                            uint totalBytes = 0;
                                            while (true)
                                            {

                                                // lê bytes da mídia e os coloca no buffer
                                                receptor.NotifySeeking();
                                                var bytesLidos = await dataReader.LoadAsync(TAMANHO_BUFFER);
                                                totalBytes += bytesLidos;
                                                var buffer = dataReader.ReadBuffer(Math.Min(bytesLidos, TAMANHO_BUFFER));
                                                BarraProgresso.Value = totalBytes;
                                                receptor.NotifySeeked();

                                                // grava o buffer no final do arquivo destino
                                                StatusTextBlock.Text = $"{DateTime.Now} - Aguarde... Gravando {FormatarTamanhoArquivo(totalBytes)} de {FormatarTamanhoArquivo(tamanhoArquivo)} em {nomeArquivo} - {string.Format("{0:0}", ((double)totalBytes / (double)tamanhoArquivo) * 100)}%";
                                                dataWriter.WriteBuffer(buffer);
                                                await dataWriter.StoreAsync();
                                                await dataWriter.FlushAsync();

                                                // ainda existem bytes a serem lídos na mídia?
                                                if (bytesLidos < TAMANHO_BUFFER)
                                                {
                                                    break;
                                                }

                                            }

                                            // finaliza a gravação de dados no arquivo destino
                                            dataWriter.DetachStream();
                                            EventosTextBox.Text += $"{DateTime.Now}    * ARQUIVO DE MÍDIA SALVO!\r\n\r\n";
                                            BarraProgresso.Value = 0;
                                            StatusTextBlock.Text = "";

                                        }
                                    }
                                    await dadosArquivo.FlushAsync();

                                }
                                receptor.NotifyEnded();

                            }

                        }
                        estaGravando = false;

                        // quer executar a cópia do arquivo LOCAL?
                        //if (!args.Stream.ContentType.Contains("image"))
                        //{

                        //    var dialogo = new MessageDialog("Quer executar a CÓPIA LOCAL da mídia salva?");
                        //    dialogo.Commands.Add(new UICommand("Sim") { Id = 0 });
                        //    dialogo.Commands.Add(new UICommand("Não") { Id = 1 });
                        //    dialogo.DefaultCommandIndex = 0;
                        //    dialogo.CancelCommandIndex = 1;
                        //    var resultado = await dialogo.ShowAsync();

                        //    if ((int)resultado.Id == 0)
                        //    {
                        //        var openPicker = new FileOpenPicker();
                        //        openPicker.FileTypeFilter.Add(".mp4");
                        //        var file = await openPicker.PickSingleFileAsync();
                        //        var stream = await file.OpenAsync(FileAccessMode.Read);
                        //        PlayerMediaElement.SetSource(stream, file.ContentType);
                        //        PlayerMediaElement.Play();
                        //    }
                        //}

                    }
                    else
                    {
                        estaGravando = false;

                        // a mídia é uma imagem
                        if (args.Stream.ContentType.Contains("image"))
                        {

                            imagemEnviada = new BitmapImage();
                            imagemEnviada.ImageOpened += imagemEnviada_ImageOpened;
                            imagemEnviada.SetSource(args.Stream);
                            if (tipoMidia != TipoMidia.Imagem)
                            {
                                if (tipoMidia == TipoMidia.AudioVideo)
                                {
                                    PlayerMediaElement.Stop();
                                }
                                ImagemImage.Opacity = 1;
                                PlayerMediaElement.Opacity = 0;
                            }
                            tipoMidia = TipoMidia.Imagem;
                        }

                        // a mídia é um vídeo ou áudio
                        else
                        {

                            // tenta abrir a mídia
                            try
                            {
                                estaMidiaCarregada = true;
                                if (!gravarArquivo)
                                {
                                    PlayerMediaElement.SetSource(args.Stream, args.Stream.ContentType);
                                }
                            }
                            catch (Exception ex)
                            {
                                EventosTextBox.Text += $"{DateTime.Now} - erro ao abrir a mídia\r\n";
                                EventosTextBox.Text += $"     {ex.Message}\r\n\r\n";
                            }

                            if (tipoMidia == TipoMidia.Imagem)
                            {
                                ImagemImage.Opacity = 0;
                                PlayerMediaElement.Opacity = 1;
                                ImagemImage.Source = null;
                            }
                            tipoMidia = TipoMidia.AudioVideo;
                        }

                    }

                }

            });

            IsPlayReceivedPreMediaLoaded = false;
        }

        // o transmissor requisitou que o receptor altere o mudo
        private async void receptor_MuteChangeRequested(PlayToReceiver recv, MuteChangeRequestedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (tipoMidia == TipoMidia.AudioVideo)
                {
                    PlayerMediaElement.IsMuted = args.Mute;
                    receptor.NotifyVolumeChange(0, args.Mute);
                }
                else if (tipoMidia == TipoMidia.Imagem)
                {
                    receptor.NotifyVolumeChange(0, args.Mute);
                }
                EventosTextBox.Text += $"{DateTime.Now} - requisitada alteração de mudo\r\n";
            });
        }

        // o transmissor requisitou que o receptor altere a taxa de execução
        private async void receptor_PlaybackRateChangeRequested(PlayToReceiver recv, PlaybackRateChangeRequestedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (tipoMidia == TipoMidia.AudioVideo)
                {
                    if (PlayerMediaElement.CurrentState != MediaElementState.Opening && PlayerMediaElement.CurrentState != MediaElementState.Closed)
                    {
                        PlayerMediaElement.PlaybackRate = args.Rate;
                        receptor.NotifyRateChange(args.Rate);
                    }
                    else
                    {
                        taxaExecucaoMidia = args.Rate;
                    }
                }
                EventosTextBox.Text += $"{DateTime.Now} - requisitada alteração na taxa de execução\r\n";
            });
        }

        // o transmissor requisitou que o receptor altere o volume
        private async void receptor_VolumeChangeRequested(PlayToReceiver recv, VolumeChangeRequestedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (tipoMidia == TipoMidia.AudioVideo)
                {
                    PlayerMediaElement.Volume = args.Volume;
                    receptor.NotifyVolumeChange(args.Volume, false);
                }
                EventosTextBox.Text += $"{DateTime.Now} - requisitada alteração no volume\r\n";
            });
        }

        #endregion

        #region - eventos no player de mídia -

        private void PlayerMediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            if (receptorIniciou)
            {
                switch (PlayerMediaElement.CurrentState)
                {
                    case MediaElementState.Playing:
                        receptor.NotifyPlaying();
                        break;

                    case MediaElementState.Paused:
                        receptor.NotifyPaused();

                        //if (estaMidiaCarregada)
                        //{
                        //    receptor.NotifyStopped();
                        //    estaMidiaCarregada = false;
                        //}
                        //else
                        //{
                        //    receptor.NotifyPaused();
                        //}
                        break;

                    case MediaElementState.Stopped:
                        receptor.NotifyStopped();
                        break;

                    default:
                        break;
                }
            }
        }

        private void PlayerMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (receptorIniciou)
            {
                receptor.NotifyEnded();
                PlayerMediaElement.Stop();
            }
        }

        private void PlayerMediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (receptorIniciou)
            {
                receptor.NotifyError();
                EventosTextBox.Text += $"{DateTime.Now} - falha ao abrir a mídia\r\n";
                EventosTextBox.Text += $"     {e.ErrorMessage}\r\n\r\n";
            }
        }

        private void PlayerMediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (receptorIniciou)
            {
                receptor.NotifyLoadedMetadata();
                receptor.NotifyDurationChange(PlayerMediaElement.NaturalDuration.TimeSpan);
                EventosTextBox.Text += $"{DateTime.Now} - arquivo de mídia aberto\r\n";
                if (IsPlayReceivedPreMediaLoaded == true)
                {
                    PlayerMediaElement.Play();
                }
            }
        }

        private void PlayerMediaElement_RateChanged(object sender, RateChangedRoutedEventArgs e)
        {
            if (receptorIniciou)
            {
                receptor.NotifyRateChange(PlayerMediaElement.PlaybackRate);
            }
        }

        private void PlayerMediaElement_SeekCompleted(object sender, RoutedEventArgs e)
        {
            if (receptorIniciou)
            {
                try
                {
                    if (!estaReposicionandoMidia)
                    {
                        receptor.NotifySeeking();
                    }
                    receptor.NotifySeeked();
                    estaReposicionandoMidia = false;
                }
                catch (InvalidOperationException ex)
                {
                    EventosTextBox.Text += $"{DateTime.Now} - falha ao reposicionar na mídia\r\n";
                    EventosTextBox.Text += $"     {ex.Message}\r\n\r\n";
                }
            }
        }

        private void PlayerMediaElement_VolumeChanged(object sender, RoutedEventArgs e)
        {
            if (receptorIniciou)
            {
                receptor.NotifyVolumeChange(PlayerMediaElement.Volume, PlayerMediaElement.IsMuted);
            }
        }

        private void PlayerMediaElement_DownloadProgressChanged(object sender, RoutedEventArgs e)
        {
            if (PlayerMediaElement.DownloadProgress == 1 && taxaExecucaoMidia > 0)
            {
                PlayerMediaElement.PlaybackRate = taxaExecucaoMidia;
                taxaExecucaoMidia = 0;
            }
        }

        #endregion

        void imagemEnviada_ImageOpened(object sender, RoutedEventArgs e)
        {
            EventosTextBox.Text += $"{DateTime.Now} - imagem aberta\r\n";
            receptor.NotifyLoadedMetadata();
        }

        private void ImagemImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            receptor.NotifyError();
            EventosTextBox.Text += $"{DateTime.Now} - falha ao carregar a imagem\r\n";
            EventosTextBox.Text += $"     {e.ErrorMessage}\r\n\r\n";
        }

        private string FormatarTamanhoArquivo(ulong size)
        {
            var units = new[] { "bytes", "KB", "MB", "GB", "TB" };
            var index = 0;
            while (size > 1024)
            {
                size /= 1024;
                index++;
            }
            return string.Format("{0:0.##} {1}", size, units[index]);

        }
    }
}
