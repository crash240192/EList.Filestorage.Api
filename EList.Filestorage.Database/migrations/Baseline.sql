create or replace function public.uuid_generate_v4()
returns uuid language 'c' cost 1 volatile strict as '$libdir/uuid-ossp', 'uuid_generate_v4';

do $CREATE_STORAGE_TYPES$
BEGIN
	if not exists (select 1 from pg_type where typname = 'storage_types')
	then 
		CREATE TYPE public.storage_types AS ENUM ('local', 'db');
	end if;
end $CREATE_STORAGE_TYPES$;

do $CREATE_STORAGE_TYPES$
BEGIN
	if not exists (select 1 from pg_type where typname = 'file_types')
	then 
		CREATE TYPE public.file_types AS ENUM ('none', 'image', 'video');
	end if;
end $CREATE_STORAGE_TYPES$;


CREATE TABLE public.file_info (
	id uuid NOT NULL DEFAULT uuid_generate_v4(),
	preview_id uuid null,
	filename varchar NOT NULL,
	"extension" varchar(10) NULL,
	content_type varchar(100) null,
	"size" int8 NOT NULL,
	storage_type storage_types not null default 'local',
	uploaded_at timestamptz NOT NULL DEFAULT NOW(),
	account_id uuid null,
	context jsonb null,
	hash varchar null,
	processing bool not null default false,
	is_available bool not null default true,
	CONSTRAINT files_pk PRIMARY KEY (id)
);

CREATE INDEX if not exists file_info_id_idx ON public.file_info (id);
CREATE INDEX if not exists file_info_accoun_id_idx ON public.file_info (account_id);

COMMENT ON COLUMN public.file_info.id IS 'Идентификатор файла';
COMMENT ON COLUMN public.file_info.filename IS 'Имя файла';
COMMENT ON COLUMN public.file_info."extension" IS 'Расширение файла';
COMMENT ON COLUMN public.file_info.content_type IS 'Тип данных';
COMMENT ON COLUMN public.file_info."size" IS 'Размер файла';
COMMENT ON COLUMN public.file_info.storage_type IS 'Тип хранилища файла';
COMMENT ON COLUMN public.file_info.uploaded_at IS 'Когда был загружен файл';
COMMENT ON COLUMN public.file_info.account_id IS 'Идентификатор пациента в системе mpi';
COMMENT ON COLUMN public.file_info.hash IS 'Хеш файла';
COMMENT ON COLUMN public.file_info.processing IS 'Флаг того что файл в данный момент в процессе обработки';
COMMENT ON COLUMN public.file_info.context IS 'Дополнительная информация';
COMMENT ON COLUMN public.file_info.is_available IS 'Флаг наличия файла в хранилище';


CREATE TABLE public.file_data (
		id uuid NOT NULL,
		"data" bytea NOT NULL,
		CONSTRAINT file_data_pk PRIMARY KEY (id),
		CONSTRAINT file_data_fk FOREIGN KEY (id) REFERENCES public.file_info(id)
);

CREATE INDEX if not exists file_data_id_idx ON public.file_data (id);


CREATE TABLE public.authorization_data (
	"token" uuid NOT NULL,
	account_id uuid NOT NULL,
	jwt_hash varchar(75) NOT NULL,
	active bool NOT NULL DEFAULT true,
	create_date timestamptz NOT NULL DEFAULT NOW(),
	update_date timestamptz NOT NULL DEFAULT NOW(),
	CONSTRAINT authorization_data_pk PRIMARY KEY ("token",jwt_hash)
);
CREATE INDEX authorization_data_token_idx ON public.authorization_data ("token",jwt_hash);
