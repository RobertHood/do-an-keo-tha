-- Visual Paradigm import version (Tools > DB > Reverse DDL, PostgreSQL)
-- JSON columns are declared as text here; change them to jsonb in the diagram after import.

CREATE TABLE users (
  id uuid NOT NULL,
  email varchar(255) NOT NULL,
  password varchar(255) NOT NULL,
  full_name varchar(150) NOT NULL,
  phone_number varchar(20),
  avatar_url varchar(500),
  role varchar(20) NOT NULL,
  is_active bool NOT NULL,
  otp_code varchar(6),
  otp_expires_at timestamp,
  hashed_refresh_token varchar(255),
  created_at timestamp NOT NULL,
  updated_at timestamp NOT NULL,
  PRIMARY KEY (id),
  UNIQUE (email)
);

CREATE TABLE feng_shui_profiles (
  id uuid NOT NULL,
  user_id uuid NOT NULL,
  birth_date date NOT NULL,
  gender varchar(10) NOT NULL,
  lunar_year int4 NOT NULL,
  can_chi varchar(30) NOT NULL,
  destiny_palace varchar(50) NOT NULL,
  fate_group varchar(20) NOT NULL,
  directions_json text NOT NULL,
  created_at timestamp NOT NULL,
  updated_at timestamp NOT NULL,
  PRIMARY KEY (id),
  UNIQUE (user_id),
  FOREIGN KEY (user_id) REFERENCES users (id)
);

CREATE TABLE design_templates (
  id uuid NOT NULL,
  name varchar(200) NOT NULL,
  style varchar(100) NOT NULL,
  suggested_plot_area float8 NOT NULL,
  preview_image_url varchar(500) NOT NULL,
  scene_config_json text NOT NULL,
  is_published bool NOT NULL,
  created_at timestamp NOT NULL,
  updated_at timestamp NOT NULL,
  PRIMARY KEY (id)
);

CREATE TABLE projects (
  id uuid NOT NULL,
  user_id uuid NOT NULL,
  template_id uuid,
  name varchar(200) NOT NULL,
  location varchar(255) NOT NULL,
  description text,
  thumbnail_url varchar(500),
  target_budget numeric(14, 2),
  init_type varchar(30) NOT NULL,
  plot_area float8,
  frontage float8,
  orientation_degree float8,
  style_preference varchar(50),
  plot_polygon text,
  scene_json text,
  created_at timestamp NOT NULL,
  updated_at timestamp NOT NULL,
  deleted_at timestamp,
  PRIMARY KEY (id),
  FOREIGN KEY (user_id) REFERENCES users (id),
  FOREIGN KEY (template_id) REFERENCES design_templates (id)
);

CREATE TABLE materials (
  id uuid NOT NULL,
  name varchar(200) NOT NULL,
  category varchar(100) NOT NULL,
  applied_zone varchar(150) NOT NULL,
  unit varchar(20) NOT NULL,
  min_unit_price numeric(12, 2) NOT NULL,
  max_unit_price numeric(12, 2) NOT NULL,
  texture_url varchar(500) NOT NULL,
  created_at timestamp NOT NULL,
  updated_at timestamp NOT NULL,
  PRIMARY KEY (id)
);

CREATE TABLE assets (
  id uuid NOT NULL,
  code varchar(100) NOT NULL,
  name varchar(200) NOT NULL,
  category varchar(100) NOT NULL,
  description text,
  loai varchar(20) NOT NULL,
  listed_price numeric(14, 2) NOT NULL,
  base_width float8 NOT NULL,
  base_height float8 NOT NULL,
  base_depth float8 NOT NULL,
  front_yaw_offset float8 NOT NULL,
  has_door bool NOT NULL,
  door_offset text,
  model_url varchar(500) NOT NULL,
  thumbnail_url varchar(500) NOT NULL,
  file_size int4 NOT NULL,
  author varchar(150),
  license varchar(150),
  is_published bool NOT NULL,
  created_at timestamp NOT NULL,
  updated_at timestamp NOT NULL,
  PRIMARY KEY (id),
  UNIQUE (code)
);

CREATE TABLE asset_materials (
  id uuid NOT NULL,
  asset_id uuid NOT NULL,
  material_id uuid NOT NULL,
  quantity float8 NOT NULL,
  created_at timestamp NOT NULL,
  updated_at timestamp NOT NULL,
  PRIMARY KEY (id),
  UNIQUE (asset_id, material_id),
  FOREIGN KEY (asset_id) REFERENCES assets (id),
  FOREIGN KEY (material_id) REFERENCES materials (id)
);

CREATE TABLE project_assets (
  id uuid NOT NULL,
  project_id uuid NOT NULL,
  asset_id uuid NOT NULL,
  position text NOT NULL,
  rotation text NOT NULL,
  scale text NOT NULL,
  is_locked bool NOT NULL,
  facing_degree float8,
  inside_plot bool NOT NULL,
  selected_store varchar(150),
  use_custom_price bool NOT NULL,
  custom_price numeric(14, 2),
  material_choices_json text,
  created_at timestamp NOT NULL,
  updated_at timestamp NOT NULL,
  PRIMARY KEY (id),
  FOREIGN KEY (project_id) REFERENCES projects (id),
  FOREIGN KEY (asset_id) REFERENCES assets (id)
);

CREATE TABLE feng_shui_audits (
  id uuid NOT NULL,
  project_id uuid NOT NULL,
  hash_bo_cuc varchar(32) NOT NULL,
  score int4 NOT NULL,
  result_json text NOT NULL,
  layout_json text NOT NULL,
  created_at timestamp NOT NULL,
  PRIMARY KEY (id),
  FOREIGN KEY (project_id) REFERENCES projects (id)
);

CREATE TABLE feng_shui_chat_histories (
  id uuid NOT NULL,
  project_id uuid NOT NULL,
  question text NOT NULL,
  answer text NOT NULL,
  hash_bo_cuc varchar(32) NOT NULL,
  citations_json text NOT NULL,
  suggestions_json text,
  model varchar(100) NOT NULL,
  created_at timestamp NOT NULL,
  PRIMARY KEY (id),
  FOREIGN KEY (project_id) REFERENCES projects (id)
);
