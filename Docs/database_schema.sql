-- PostgreSQL schema for Visual Paradigm: Tools > DB > Reverse DDL...
-- Note: role stored as VARCHAR(20) (values of the Role enum) so the DDL parser accepts it.

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
  id                      UUID         NOT NULL,
  user_id                 UUID         NOT NULL,
  birth_year              INTEGER      NOT NULL,
  gender                  VARCHAR(10)  NOT NULL,
  kua_number              INTEGER      NOT NULL,
  destiny_palace          VARCHAR(50)  NOT NULL,
  fate_group              VARCHAR(20)  NOT NULL,
  auspicious_directions   JSONB        NOT NULL,
  inauspicious_directions JSONB        NOT NULL,
  created_at              TIMESTAMP    NOT NULL,
  updated_at              TIMESTAMP    NOT NULL,
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
  id            UUID           NOT NULL,
  user_id       UUID           NOT NULL,
  template_id   UUID,
  name          VARCHAR(200)   NOT NULL,
  location      VARCHAR(255)   NOT NULL,
  description   TEXT,
  thumbnail_url VARCHAR(500),
  target_budget DECIMAL(14,2),
  init_type     VARCHAR(30)    NOT NULL,
  scene_json    JSONB,
  created_at    TIMESTAMP      NOT NULL,
  updated_at    TIMESTAMP      NOT NULL,
  deleted_at    TIMESTAMP,
  CONSTRAINT pk_projects PRIMARY KEY (id)
);

CREATE TABLE plots (
  id                 UUID              NOT NULL,
  project_id         UUID              NOT NULL,
  width              DOUBLE PRECISION  NOT NULL,
  depth              DOUBLE PRECISION  NOT NULL,
  area               DOUBLE PRECISION  NOT NULL,
  orientation_degree DOUBLE PRECISION  NOT NULL,
  orientation_label  VARCHAR(30)       NOT NULL,
  boundary_polygon   JSONB,
  created_at         TIMESTAMP         NOT NULL,
  updated_at         TIMESTAMP         NOT NULL,
  CONSTRAINT pk_plots PRIMARY KEY (id),
  CONSTRAINT uq_plots_project UNIQUE (project_id)
);

CREATE TABLE house_structures (
  id               UUID              NOT NULL,
  project_id       UUID              NOT NULL,
  house_model_name VARCHAR(200)      NOT NULL,
  footprint_width  DOUBLE PRECISION  NOT NULL,
  footprint_depth  DOUBLE PRECISION  NOT NULL,
  footprint_area   DOUBLE PRECISION  NOT NULL,
  floor_count      INTEGER           NOT NULL,
  total_floor_area DOUBLE PRECISION  NOT NULL,
  front_setback    DOUBLE PRECISION  NOT NULL,
  side_setback     DOUBLE PRECISION  NOT NULL,
  rear_setback     DOUBLE PRECISION  NOT NULL,
  facing_direction VARCHAR(30)       NOT NULL,
  created_at       TIMESTAMP         NOT NULL,
  updated_at       TIMESTAMP         NOT NULL,
  CONSTRAINT pk_house_structures PRIMARY KEY (id),
  CONSTRAINT uq_house_structures_project UNIQUE (project_id)
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

CREATE TABLE project_materials (
  id              UUID              NOT NULL,
  project_id      UUID              NOT NULL,
  material_id     UUID              NOT NULL,
  applied_surface VARCHAR(100)      NOT NULL,
  quantity_area   DOUBLE PRECISION  NOT NULL,
  created_at      TIMESTAMP         NOT NULL,
  updated_at      TIMESTAMP         NOT NULL,
  CONSTRAINT pk_project_materials PRIMARY KEY (id)
);

CREATE TABLE feng_shui_audits (
  id                      UUID       NOT NULL,
  project_id              UUID       NOT NULL,
  overview_table_json     JSONB      NOT NULL,
  layout_suggestions_json JSONB      NOT NULL,
  created_at              TIMESTAMP  NOT NULL,
  CONSTRAINT pk_feng_shui_audits PRIMARY KEY (id)
);

CREATE TABLE feng_shui_chat_histories (
  id         UUID       NOT NULL,
  project_id UUID       NOT NULL,
  question   TEXT       NOT NULL,
  answer     TEXT       NOT NULL,
  created_at TIMESTAMP  NOT NULL,
  CONSTRAINT pk_feng_shui_chat_histories PRIMARY KEY (id)
);

-- Foreign keys (each becomes a relationship line in the ERD)
ALTER TABLE feng_shui_profiles       ADD CONSTRAINT fk_profiles_user      FOREIGN KEY (user_id)     REFERENCES users (id);
ALTER TABLE projects                 ADD CONSTRAINT fk_projects_user      FOREIGN KEY (user_id)     REFERENCES users (id);
ALTER TABLE projects                 ADD CONSTRAINT fk_projects_template  FOREIGN KEY (template_id) REFERENCES design_templates (id);
ALTER TABLE plots                    ADD CONSTRAINT fk_plots_project      FOREIGN KEY (project_id)  REFERENCES projects (id);
ALTER TABLE house_structures         ADD CONSTRAINT fk_house_project      FOREIGN KEY (project_id)  REFERENCES projects (id);
ALTER TABLE project_materials        ADD CONSTRAINT fk_pm_project         FOREIGN KEY (project_id)  REFERENCES projects (id);
ALTER TABLE project_materials        ADD CONSTRAINT fk_pm_material        FOREIGN KEY (material_id) REFERENCES materials (id);
ALTER TABLE feng_shui_audits         ADD CONSTRAINT fk_audits_project     FOREIGN KEY (project_id)  REFERENCES projects (id);
ALTER TABLE feng_shui_chat_histories ADD CONSTRAINT fk_chats_project      FOREIGN KEY (project_id)  REFERENCES projects (id);
