
CREATE INDEX concurrently track_album_id_idx ON track (album_id);

CREATE INDEX concurrently track_genre_id_idx ON track (genre_id);

CREATE INDEX concurrently track_media_type_id_idx ON track (media_type_id);