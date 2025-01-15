FROM python:3
WORKDIR /app
RUN pip install Flask
COPY src/ .
COPY printflag /printflag
RUN chmod +x /printflag && chmod 0100 /printflag
CMD [ "python3", "app.py" ]