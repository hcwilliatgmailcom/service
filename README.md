-- connect as autorest
CREATE TABLE autorest.person (
  id NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  name VARCHAR2(100) NOT NULL
);
