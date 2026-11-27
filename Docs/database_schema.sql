-- SuburbSim - Backend Core (PostgreSQL), 10 bảng
-- Visual Paradigm: Tools > DB > Reverse DDL... (Database = PostgreSQL)
-- Ghi chú: role lưu dạng VARCHAR(20) (giá trị của enum Role: HOMEOWNER, ARCHITECT, CONTRACTOR, ADMIN)
-- để trình đọc DDL của Visual Paradigm không báo lỗi với CREATE TYPE ... AS ENUM.

CREATE TABLE users (
  id                   UUID          NOT NULL,
  email                VARCHAR(255)  NOT NULL,
  password             VARCHAR(255)  NOT NULL,
  full_name            VARCHAR(150)  NOT NULL,
  phone_number         VARCHAR(20),
  avatar_url           VARCHAR(500),
  role                 VARCHAR(20)   NOT NULL,
  is_active            BOOLEAN       NOT NULL,
  otp_code             VARCHAR(6),
  otp_expires_at       TIMESTAMP,
  hashed_refresh_token VARCHAR(255),
  created_at           TIMESTAMP     NOT NULL,
  updated_at           TIMESTAMP     NOT NULL,
  CONSTRAINT pk_users PRIMARY KEY (id),
  CONSTRAINT uq_users_email UNIQUE (email)
);

CREATE TABLE feng_shui_profiles (
  id              UUID         NOT NULL,
  user_id         UUID         NOT NULL,
  birth_date      DATE         NOT NULL,
  gender          VARCHAR(10)  NOT NULL,
  lunar_year      INTEGER      NOT NULL,
  can_chi         VARCHAR(30)  NOT NULL,
  destiny_palace  VARCHAR(50)  NOT NULL,
  fate_group      VARCHAR(20)  NOT NULL,
  directions_json JSONB        NOT NULL,
  created_at      TIMESTAMP    NOT NULL,
  updated_at      TIMESTAMP    NOT NULL,
  CONSTRAINT pk_feng_shui_profiles PRIMARY KEY (id),
  CONSTRAINT uq_feng_shui_profiles_user UNIQUE (user_id)
);

CREATE TABLE design_templates (
  id                  UUID              NOT NULL,
  name                VARCHAR(200)      NOT NULL,
  style               VARCHAR(100)      NOT NULL,
  suggested_plot_area DOUBLE PRECISION  NOT NULL,
  preview_image_url   VARCHAR(500)      NOT NULL,
  scene_config_json   JSONB             NOT NULL,
  is_published        BOOLEAN           NOT NULL,
  created_at          TIMESTAMP         NOT NULL,
  updated_at          TIMESTAMP         NOT NULL,
  CONSTRAINT pk_design_templates PRIMARY KEY (id)
);

CREATE TABLE projects (
  id                 UUID              NOT NULL,
  user_id            UUID              NOT NULL,
  template_id        UUID,
  name               VARCHAR(200)      NOT NULL,
  location           VARCHAR(255)      NOT NULL,
  description        TEXT,
  thumbnail_url      VARCHAR(500),
  target_budget      DECIMAL(14,2),
  init_type          VARCHAR(30)       NOT NULL,
  plot_area          DOUBLE PRECISION,
  frontage           DOUBLE PRECISION,
  orientation_degree DOUBLE PRECISION,
  style_preference   VARCHAR(50),
  plot_polygon       JSONB,
  scene_json         JSONB,
  created_at         TIMESTAMP         NOT NULL,
  updated_at         TIMESTAMP         NOT NULL,
  deleted_at         TIMESTAMP,
  CONSTRAINT pk_projects PRIMARY KEY (id)
);

CREATE TABLE materials (
  id             UUID           NOT NULL,
  name           VARCHAR(200)   NOT NULL,
  category       VARCHAR(100)   NOT NULL,
  applied_zone   VARCHAR(150)   NOT NULL,
  unit           VARCHAR(20)    NOT NULL,
  min_unit_price DECIMAL(12,2)  NOT NULL,
  max_unit_price DECIMAL(12,2)  NOT NULL,
  texture_url    VARCHAR(500)   NOT NULL,
  created_at     TIMESTAMP      NOT NULL,
  updated_at     TIMESTAMP      NOT NULL,
  CONSTRAINT pk_materials PRIMARY KEY (id)
);

CREATE TABLE assets (
  id               UUID              NOT NULL,
  code             VARCHAR(100)      NOT NULL,
  name             VARCHAR(200)      NOT NULL,
  category         VARCHAR(100)      NOT NULL,
  description      TEXT,
  loai             VARCHAR(20)       NOT NULL,
  listed_price     DECIMAL(14,2)     NOT NULL,
  base_width       DOUBLE PRECISION  NOT NULL,
  base_height      DOUBLE PRECISION  NOT NULL,
  base_depth       DOUBLE PRECISION  NOT NULL,
  front_yaw_offset DOUBLE PRECISION  NOT NULL,
  has_door         BOOLEAN           NOT NULL,
  door_offset      JSONB,
  model_url        VARCHAR(500)      NOT NULL,
  thumbnail_url    VARCHAR(500)      NOT NULL,
  file_size        INTEGER           NOT NULL,
  author           VARCHAR(150),
  license          VARCHAR(150),
  is_published     BOOLEAN           NOT NULL,
  created_at       TIMESTAMP         NOT NULL,
  updated_at       TIMESTAMP         NOT NULL,
  CONSTRAINT pk_assets PRIMARY KEY (id),
  CONSTRAINT uq_assets_code UNIQUE (code)
);

CREATE TABLE asset_materials (
  id          UUID              NOT NULL,
  asset_id    UUID              NOT NULL,
  material_id UUID              NOT NULL,
  quantity    DOUBLE PRECISION  NOT NULL,
  created_at  TIMESTAMP         NOT NULL,
  updated_at  TIMESTAMP         NOT NULL,
  CONSTRAINT pk_asset_materials PRIMARY KEY (id),
  CONSTRAINT uq_asset_materials UNIQUE (asset_id, material_id)
);

CREATE TABLE project_assets (
  id                    UUID              NOT NULL,
  project_id            UUID              NOT NULL,
  asset_id              UUID              NOT NULL,
  position              JSONB             NOT NULL,
  rotation              JSONB             NOT NULL,
  scale                 JSONB             NOT NULL,
  is_locked             BOOLEAN           NOT NULL,
  facing_degree         DOUBLE PRECISION,
  inside_plot           BOOLEAN           NOT NULL,
  selected_store        VARCHAR(150),
  use_custom_price      BOOLEAN           NOT NULL,
  custom_price          DECIMAL(14,2),
  material_choices_json JSONB,
  created_at            TIMESTAMP         NOT NULL,
  updated_at            TIMESTAMP         NOT NULL,
  CONSTRAINT pk_project_assets PRIMARY KEY (id)
);

CREATE TABLE feng_shui_audits (
  id          UUID         NOT NULL,
  project_id  UUID         NOT NULL,
  hash_bo_cuc VARCHAR(32)  NOT NULL,
  score       INTEGER      NOT NULL,
  result_json JSONB        NOT NULL,
  layout_json JSONB        NOT NULL,
  created_at  TIMESTAMP    NOT NULL,
  CONSTRAINT pk_feng_shui_audits PRIMARY KEY (id)
);

CREATE TABLE feng_shui_chat_histories (
  id               UUID          NOT NULL,
  project_id       UUID          NOT NULL,
  question         TEXT          NOT NULL,
  answer           TEXT          NOT NULL,
  hash_bo_cuc      VARCHAR(32)   NOT NULL,
  citations_json   JSONB         NOT NULL,
  suggestions_json JSONB,
  model            VARCHAR(100)  NOT NULL,
  created_at       TIMESTAMP     NOT NULL,
  CONSTRAINT pk_feng_shui_chat_histories PRIMARY KEY (id)
);

-- Khóa ngoại (mỗi dòng thành một đường quan hệ trong ERD)
ALTER TABLE feng_shui_profiles       ADD CONSTRAINT fk_profiles_user     FOREIGN KEY (user_id)     REFERENCES users (id)            ON DELETE CASCADE;
ALTER TABLE projects                 ADD CONSTRAINT fk_projects_user     FOREIGN KEY (user_id)     REFERENCES users (id)            ON DELETE CASCADE;
ALTER TABLE projects                 ADD CONSTRAINT fk_projects_template FOREIGN KEY (template_id) REFERENCES design_templates (id) ON DELETE SET NULL;
ALTER TABLE asset_materials          ADD CONSTRAINT fk_am_asset          FOREIGN KEY (asset_id)    REFERENCES assets (id)           ON DELETE CASCADE;
ALTER TABLE asset_materials          ADD CONSTRAINT fk_am_material       FOREIGN KEY (material_id) REFERENCES materials (id)        ON DELETE RESTRICT;
ALTER TABLE project_assets           ADD CONSTRAINT fk_pa_project        FOREIGN KEY (project_id)  REFERENCES projects (id)         ON DELETE CASCADE;
ALTER TABLE project_assets           ADD CONSTRAINT fk_pa_asset          FOREIGN KEY (asset_id)    REFERENCES assets (id)           ON DELETE RESTRICT;
ALTER TABLE feng_shui_audits         ADD CONSTRAINT fk_audits_project   FOREIGN KEY (project_id)  REFERENCES projects (id)         ON DELETE CASCADE;
ALTER TABLE feng_shui_chat_histories ADD CONSTRAINT fk_chats_project     FOREIGN KEY (project_id)  REFERENCES projects (id)         ON DELETE CASCADE;
