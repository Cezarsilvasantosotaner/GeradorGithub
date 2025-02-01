# GeradorGithub
Gerador da minha página no GitHub Pagesfrom flask import Flask, render_template_string
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler
import time

app = Flask(__name__)

# Configurações do arquivo de log
LOG_FILE_PATH = '/var/log/apache2/access.log'

class LogHandler(FileSystemEventHandler):
    def on_modified(self, event):
        if event.src_path == LOG_FILE_PATH:
            with open(LOG_FILE_PATH, 'r') as file:
                lines = file.readlines()
                # Aqui você pode processar as linhas do log como desejar
                print("Log atualizado:", lines[-10:])  # Exibe as últimas 10 linhas

@app.route('/')
def index():
    return render_template_string("""
    <h1>Monitor de Logs do Apache</h1>
    <p>O aplicativo está monitorando o arquivo de log: {{ log_file }}</p>
    """, log_file=LOG_FILE_PATH)

if __name__ == '__main__':
    # Inicia o monitoramento do arquivo de log
    event_handler = LogHandler()
    observer = Observer()
    observer.schedule(event_handler, path=LOG_FILE_PATH, recursive=False)
    observer.start()

    try:
        app.run(debug=True)
    except KeyboardInterrupt:
        observer.stop()
    observer.join()

