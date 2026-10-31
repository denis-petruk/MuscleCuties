-- Canonical catalog seed for a fresh MuscleCuties database.
--
-- Generated from DbMaintenance seed of CanonicalCatalogManifest.json
-- (SHA-256 927597abafdd735b01d704462b66d0e76997aaa98a5ff5336dfef8bd87fdd0b5, 742 rows).
-- Apply as musclecuties_migrator after schema.sql and roles-and-grants.sql.
-- Explicit identity values are followed by sequence resets so later inserts
-- cannot collide with seeded ids.

--
-- PostgreSQL database dump
--


-- Dumped from database version 17.11
-- Dumped by pg_dump version 17.11

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Data for Name: workout_exercise_definitions; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.workout_exercise_definitions VALUES (1, 'urn:musclecuties:seed:v1:358a747d-530f-42b2-ab2c-409617296398', 'Barbell Hip Thrust', 'HipThrust', 'Barbell, HipThrustBench', 1, 3, 2, 180, 4, true, false, false, 'None', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (2, 'urn:musclecuties:seed:v1:6cfadb0c-f65c-4908-8df9-ddd3afefbb14', 'Machine Hip Thrust', 'HipThrust', 'Machines', 1, 3, 1, 60, 4, true, false, false, 'None', 'Metatarsal, Ankle, Neck', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (3, 'urn:musclecuties:seed:v1:80857624-d6da-4a17-8a2e-e0f6baa0cb3e', 'B-Stance Hip Thrust', 'HipThrust', 'Barbell, HipThrustBench', 1, 2, 2, 150, 4, false, true, false, 'None', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (4, 'urn:musclecuties:seed:v1:4de35920-d0f2-476d-9cd8-8aa7adac186b', 'Single-Leg Glute Bridge', 'HipThrust', 'None', 1, 1, 1, 30, 4, false, true, true, 'None', 'Knee, LowBack, Neck', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (5, 'urn:musclecuties:seed:v1:8e88ec8f-6c9c-4cbd-9e91-b952eaa820c6', 'Cable Pull-Through', 'HipHinge', 'Cables', 2, 2, 1, 60, 4, false, false, false, 'LowBack', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (6, 'urn:musclecuties:seed:v1:cda7b561-d767-4ab8-a9fd-4cda202728f8', '45 Degree Back Extension', 'HipHinge', 'BackExtension45', 2, 2, 2, 45, 4, false, false, false, 'LowBack', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (7, 'urn:musclecuties:seed:v1:89d855f5-52d2-45f6-a87a-f356f60bb96d', 'Kneeling Cable Kickback', 'HipThrust', 'Cables', 1, 1, 1, 60, 4, false, true, false, 'Knee', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (8, 'urn:musclecuties:seed:v1:96f105c5-fc8e-46e9-9cfc-ed828ee29920', 'Romanian Deadlift', 'HipHinge', 'Barbell', 2, 4, 2, 180, 5, true, false, false, 'LowBack', 'None', 0, 2, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (9, 'urn:musclecuties:seed:v1:adf7ba2e-cef3-4226-8645-ff8ee1d84297', 'Deficit RDL', 'HipHinge', 'Barbell', 2, 4, 3, 180, 5, true, false, false, 'LowBack', 'None', 0, 2, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (10, 'urn:musclecuties:seed:v1:ee6fcfd0-36c5-4ad1-b103-f9cdb63f1407', 'Dumbbell RDL', 'HipHinge', 'Dumbbell', 2, 3, 2, 60, 4, false, false, false, 'LowBack', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (11, 'urn:musclecuties:seed:v1:ba089d86-7cda-41ff-bb41-162bb3ec8db3', 'Single-Leg RDL', 'HipHinge', 'Dumbbell', 2, 2, 3, 60, 4, false, true, false, 'Ankle', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (12, 'urn:musclecuties:seed:v1:15e985d8-a319-4f8a-8056-b0c9740bc06e', 'Seated Leg Curl', 'KneeFlexion', 'Machines', 2, 2, 1, 45, 4, false, false, false, 'None', 'Metatarsal, Knee, Ankle, Wrist, Hip', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (13, 'urn:musclecuties:seed:v1:66ad4def-bc34-46a8-aa37-dcd399a9aa4a', 'Lying Leg Curl', 'KneeFlexion', 'Machines', 1, 2, 1, 45, 4, false, false, false, 'None', 'Metatarsal, Knee, Ankle, Wrist, Hip', 2, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (14, 'urn:musclecuties:seed:v1:c7ff7b35-08ee-4d55-bf6e-5cf85a7ef1a8', 'Nordic Curl', 'KneeFlexion', 'None', 2, 4, 3, 60, 5, false, false, true, 'Knee', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (15, 'urn:musclecuties:seed:v1:15e112b9-686e-4636-b1a1-13f110dafa50', 'Good Morning', 'HipHinge', 'Barbell', 2, 4, 3, 180, 5, false, false, false, 'LowBack', 'None', 0, 2, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (16, 'urn:musclecuties:seed:v1:02ae85ce-db74-4695-9b4c-52ca41311dac', 'Back Squat', 'SquatPattern', 'Barbell', 2, 5, 3, 180, 5, true, false, false, 'Knee, LowBack', 'None', 0, 2, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (17, 'urn:musclecuties:seed:v1:b7ef9295-e9ca-44a2-9c27-44e8086b40d0', 'Hack Squat', 'SquatPattern', 'Machines', 2, 4, 1, 60, 5, true, false, false, 'Knee', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (18, 'urn:musclecuties:seed:v1:91b84838-50c6-4a81-98b8-3e898d624964', 'Leg Press', 'SquatPattern', 'Machines', 1, 3, 1, 60, 4, true, false, false, 'Knee', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (19, 'urn:musclecuties:seed:v1:cf2e288c-370c-441d-b806-4a59b152bc3d', 'Goblet Squat', 'SquatPattern', 'Dumbbell', 2, 2, 1, 45, 4, false, false, false, 'Knee', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (20, 'urn:musclecuties:seed:v1:a98580cb-9167-45cc-96d8-6b38a53adc26', 'Bulgarian Split Squat', 'Lunge', 'Dumbbell', 2, 3, 2, 60, 4, false, true, false, 'Knee, Ankle', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (21, 'urn:musclecuties:seed:v1:28719fdf-acd5-46ed-ad02-3a9969eef3d1', 'Walking Lunge', 'Lunge', 'Dumbbell', 1, 3, 2, 60, 4, false, true, false, 'Metatarsal, Knee', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (22, 'urn:musclecuties:seed:v1:a42eb2dc-d85c-4188-ace8-31b566917759', 'Step-Up', 'Lunge', 'Dumbbell', 1, 2, 2, 60, 4, false, true, false, 'Knee', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (23, 'urn:musclecuties:seed:v1:5c8143d0-0806-4dc7-9160-245bc3ad7cb4', 'Reverse Lunge', 'Lunge', 'Dumbbell', 1, 2, 1, 60, 4, false, true, false, 'Knee', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (24, 'urn:musclecuties:seed:v1:2f8c4d9e-aeb9-4d36-9990-6cdab189b855', 'Seated Hip Abduction Lean', 'HipAbduction', 'Machines', 1, 1, 1, 45, 4, false, false, false, 'None', 'Metatarsal, Knee, Ankle', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (25, 'urn:musclecuties:seed:v1:f8db9aa0-7e97-4f1d-801e-a2a10aff1686', 'Seated Hip Abduction Upright', 'HipAbduction', 'Machines', 1, 1, 1, 45, 4, false, false, false, 'None', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (26, 'urn:musclecuties:seed:v1:d8cd729e-6d1a-4460-ae57-08a765195053', 'Standing Cable Abduction', 'HipAbduction', 'Cables', 1, 1, 1, 60, 4, false, true, false, 'None', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (27, 'urn:musclecuties:seed:v1:4de9d407-990b-4054-8fb3-ec5583a95222', 'Banded Lateral Walk', 'HipAbduction', 'Bands', 0, 1, 1, 20, 3, false, false, false, 'None', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (28, 'urn:musclecuties:seed:v1:95236114-7c53-4f92-bcb4-07dc86cdedab', 'Side-Lying Abduction', 'HipAbduction', 'None', 1, 1, 1, 20, 4, false, true, true, 'None', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (29, 'urn:musclecuties:seed:v1:f8316c0a-b670-4746-8042-ddc01f6a106d', 'Copenhagen Plank', 'HipAdduction', 'None', 1, 2, 3, 20, 4, false, true, true, 'Hip', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (30, 'urn:musclecuties:seed:v1:93e52d26-71d4-4af0-9186-ace719797d1b', 'Seated Adduction Machine', 'HipAdduction', 'Machines', 1, 1, 1, 45, 4, false, false, false, 'None', 'Metatarsal, Ankle', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (31, 'urn:musclecuties:seed:v1:078615bb-9882-4aaf-a3ec-5c74af6fdced', 'Pull-Up', 'VerticalPull', 'PullUpBar', 2, 3, 2, 30, 4, true, false, true, 'Shoulder', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (32, 'urn:musclecuties:seed:v1:d929aa0a-2a73-457c-9fd0-2ea031156f03', 'Lat Pulldown', 'VerticalPull', 'Machines, Cables', 2, 2, 1, 45, 4, true, false, false, 'None', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (33, 'urn:musclecuties:seed:v1:99ad2575-3649-447c-b50b-d46e3fb38bc5', 'Chest-Supported Row', 'HorizontalPull', 'Dumbbell, Machines', 1, 2, 1, 60, 4, true, false, false, 'None', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (34, 'urn:musclecuties:seed:v1:84ec45d0-e1e7-453d-a7f0-c3ee41823bde', 'Seated Cable Row', 'HorizontalPull', 'Cables', 1, 2, 1, 45, 4, true, false, false, 'LowBack', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (35, 'urn:musclecuties:seed:v1:66ff4d88-e1e7-4d32-9d54-f49d6cfcb85a', 'Single-Arm DB Row', 'HorizontalPull', 'Dumbbell', 1, 2, 1, 60, 4, false, true, false, 'LowBack', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (36, 'urn:musclecuties:seed:v1:d49bb2b1-0d37-4294-9d08-552b1c0c735d', 'Straight-Arm Pulldown', 'VerticalPull', 'Cables', 2, 1, 1, 45, 4, false, false, false, 'Shoulder', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (37, 'urn:musclecuties:seed:v1:92c0d019-602e-46d7-ae78-4cb6de508481', 'Face Pull', 'RearDelt', 'Cables', 1, 1, 1, 45, 4, false, false, false, 'None', 'Shoulder, Wrist, Hip', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (38, 'urn:musclecuties:seed:v1:0de4b1cf-3328-4957-83fa-9b43c5da6318', 'DB Shoulder Press', 'VerticalPush', 'Dumbbell', 1, 3, 2, 60, 4, true, false, false, 'Shoulder', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (39, 'urn:musclecuties:seed:v1:31967a86-690e-4530-bbc4-02417d27f463', 'Machine Shoulder Press', 'VerticalPush', 'Machines', 1, 2, 1, 45, 4, true, false, false, 'Shoulder', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (40, 'urn:musclecuties:seed:v1:be7ea79e-2002-47de-aaea-98043b5dede5', 'Incline DB Press', 'HorizontalPush', 'Dumbbell', 2, 3, 2, 60, 4, true, false, false, 'Shoulder', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (41, 'urn:musclecuties:seed:v1:82839d75-d149-4283-901f-b89369fe490a', 'Machine Chest Press', 'HorizontalPush', 'Machines', 1, 2, 1, 45, 4, true, false, false, 'Shoulder', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (42, 'urn:musclecuties:seed:v1:d5a88b8d-c3e7-4a9b-8b23-439e65ed44e6', 'Push-Up', 'HorizontalPush', 'None', 1, 2, 1, 15, 4, false, false, true, 'Shoulder, Wrist', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (43, 'urn:musclecuties:seed:v1:33723cec-43c4-4863-843d-ac2fd941bacb', 'Cable Lateral Raise', 'LateralRaise', 'Cables', 2, 1, 1, 45, 4, false, true, false, 'Shoulder', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (44, 'urn:musclecuties:seed:v1:89f96b37-d408-47f0-a14a-4bea7f61ad9d', 'DB Lateral Raise', 'LateralRaise', 'Dumbbell', 1, 1, 1, 30, 4, false, false, false, 'Shoulder', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (45, 'urn:musclecuties:seed:v1:1b0cd172-0f22-468d-be48-f5376d3685ef', 'Reverse Pec Deck', 'RearDelt', 'Machines', 1, 1, 1, 45, 4, false, false, false, 'None', 'Shoulder, Wrist', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (46, 'urn:musclecuties:seed:v1:f1458e45-8be5-4f7c-bac2-3366bb44b359', 'Incline DB Curl', 'ElbowFlexion', 'Dumbbell', 2, 1, 1, 45, 4, false, false, false, 'None', 'Shoulder', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (47, 'urn:musclecuties:seed:v1:b616911c-3924-4ef5-b149-17d3489cb266', 'Cable Curl', 'ElbowFlexion', 'Cables', 1, 1, 1, 45, 4, false, false, false, 'None', 'Shoulder', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (48, 'urn:musclecuties:seed:v1:01754aa0-949f-4096-a1a1-1b81cc38cadd', 'Overhead Cable Extension', 'ElbowExtension', 'Cables', 2, 1, 1, 45, 4, false, false, false, 'Shoulder', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (49, 'urn:musclecuties:seed:v1:186a19b4-7135-4018-a1a9-74659a8bfb8f', 'Rope Pushdown', 'ElbowExtension', 'Cables', 1, 1, 1, 45, 4, false, false, false, 'None', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (50, 'urn:musclecuties:seed:v1:de2c023b-2057-458a-909f-d2ec91dc4adf', 'Dead Bug', 'AntiExtension', 'None', 0, 1, 1, 15, 4, false, false, true, 'None', 'LowBack, Hip', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (51, 'urn:musclecuties:seed:v1:3379c844-850c-4c03-b79b-91a8fc68fa23', 'Ab Wheel Rollout', 'AntiExtension', 'None', 2, 2, 3, 20, 5, false, false, true, 'Shoulder, LowBack', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (52, 'urn:musclecuties:seed:v1:ae68df60-cfa6-4bed-b82f-16ff3e3d9b04', 'Hollow Hold', 'AntiExtension', 'None', 0, 1, 2, 15, 0, false, false, true, 'Neck', 'None', 1, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (53, 'urn:musclecuties:seed:v1:107b6742-e8ae-4f10-8cd9-2dc3515dba23', 'Cable Crunch', 'AntiExtension', 'Cables', 1, 2, 1, 45, 4, false, false, false, 'Neck', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (54, 'urn:musclecuties:seed:v1:26e42f06-ec22-4cc3-b1eb-bc9a33656fcb', 'Pallof Press', 'AntiRotation', 'Cables', 0, 1, 1, 45, 4, false, true, false, 'None', 'LowBack, Hip, Neck', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (55, 'urn:musclecuties:seed:v1:46c57466-86b2-4185-acdd-1f85c2fd2cd5', 'Bird Dog', 'AntiRotation', 'None', 0, 1, 1, 15, 4, false, true, true, 'None', 'LowBack, Neck', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (56, 'urn:musclecuties:seed:v1:67f21fe0-d014-4675-b3f1-6870e733f6fe', 'Side Plank', 'AntiLateralFlexion', 'None', 0, 1, 1, 15, 0, false, true, true, 'Shoulder', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (57, 'urn:musclecuties:seed:v1:2cb0016f-6f01-4c60-8730-b6b142a4c970', 'Suitcase Carry', 'AntiLateralFlexion', 'Dumbbell', 0, 2, 1, 45, 0, false, true, false, 'None', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (58, 'urn:musclecuties:seed:v1:c2b89e0d-a14e-469c-af64-2bb2d48c3a70', 'Standing Calf Raise', 'CalfRaise', 'Machines', 2, 1, 1, 45, 3, false, false, false, 'Metatarsal, Ankle', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (59, 'urn:musclecuties:seed:v1:df944a05-009f-4ce2-ad0f-5e03d7efc6d5', 'Seated Calf Raise', 'CalfRaise', 'Machines', 2, 1, 1, 45, 3, false, false, false, 'Metatarsal', 'None', 0, 0, NULL);
INSERT INTO catalog.workout_exercise_definitions VALUES (60, 'urn:musclecuties:seed:v1:7276be74-7fab-4b0c-b42f-0e232dd408b8', 'Rock Climbing', 'Climbing', 'ClimbingWall', 1, 4, 2, 0, 0, false, false, false, 'Shoulder, Wrist', 'None', 0, 0, NULL);


--
-- Data for Name: workout_muscle_groups; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.workout_muscle_groups VALUES (1, 'urn:musclecuties:seed:v1:2482082f-8357-4272-a6a6-8f86fd709353', 'Glutes');
INSERT INTO catalog.workout_muscle_groups VALUES (2, 'urn:musclecuties:seed:v1:229819bd-bdd8-4d33-954e-1676c6e6ffc0', 'Hamstrings');
INSERT INTO catalog.workout_muscle_groups VALUES (3, 'urn:musclecuties:seed:v1:f9b0e51e-69e1-4b77-946d-e43fa5569196', 'Abductors');
INSERT INTO catalog.workout_muscle_groups VALUES (4, 'urn:musclecuties:seed:v1:4ccdb813-0416-4c08-8de4-00077dabec5b', 'Adductors');
INSERT INTO catalog.workout_muscle_groups VALUES (5, 'urn:musclecuties:seed:v1:06b10b17-0b56-44db-9299-7995413b856e', 'Quads');
INSERT INTO catalog.workout_muscle_groups VALUES (6, 'urn:musclecuties:seed:v1:022fe7f6-a8b3-4cbb-a27c-409e2d768f5b', 'Lats');
INSERT INTO catalog.workout_muscle_groups VALUES (7, 'urn:musclecuties:seed:v1:e9b279e3-acee-42b4-b780-4d39ad2243f3', 'UpperBack');
INSERT INTO catalog.workout_muscle_groups VALUES (8, 'urn:musclecuties:seed:v1:b2793554-ca83-40b0-8173-4b10204a6c1e', 'Delts');
INSERT INTO catalog.workout_muscle_groups VALUES (9, 'urn:musclecuties:seed:v1:5d46f4d2-8a4f-41ef-9a3e-cd75cb3dd65d', 'Chest');
INSERT INTO catalog.workout_muscle_groups VALUES (10, 'urn:musclecuties:seed:v1:518ee424-4558-4f95-810d-269b027a2dab', 'Biceps');
INSERT INTO catalog.workout_muscle_groups VALUES (11, 'urn:musclecuties:seed:v1:e57082b5-0120-4e5b-9a0e-be7ef3ff3514', 'Triceps');
INSERT INTO catalog.workout_muscle_groups VALUES (12, 'urn:musclecuties:seed:v1:d54b928a-7764-4b9e-99bf-a70618b5f5f0', 'Core');
INSERT INTO catalog.workout_muscle_groups VALUES (13, 'urn:musclecuties:seed:v1:ae249c0c-236d-4757-890f-65afb9e6701a', 'Calves');


--
-- Data for Name: exercise_muscle_contributions; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.exercise_muscle_contributions VALUES (1, 'urn:musclecuties:seed:v1:2cb35059-9035-451c-95e9-c983d3b71f21', 1, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (2, 'urn:musclecuties:seed:v1:75b01217-d441-490f-9650-727d55876064', 1, 2, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (3, 'urn:musclecuties:seed:v1:47554ace-a5bb-4b03-8f6e-5d492e033998', 2, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (4, 'urn:musclecuties:seed:v1:cbeb6d3b-5c81-4db4-a5d7-1309e1e2d375', 2, 2, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (5, 'urn:musclecuties:seed:v1:ed22d728-82c0-4da5-8337-9ee044dc6618', 3, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (6, 'urn:musclecuties:seed:v1:9f9bd835-a57f-4a47-b255-87a75b6dc15b', 3, 2, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (7, 'urn:musclecuties:seed:v1:a53b1b52-4225-4cdc-92dd-2c7e18083e9d', 4, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (8, 'urn:musclecuties:seed:v1:8fdc2caa-5fa2-4fc8-8929-7c96b162d0f8', 4, 2, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (9, 'urn:musclecuties:seed:v1:23ddd67e-cdaa-4487-a9b8-0a8699317ba3', 5, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (10, 'urn:musclecuties:seed:v1:3c542648-c512-4496-84f9-f73cad310e3c', 5, 2, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (11, 'urn:musclecuties:seed:v1:1ecd7a5d-7c92-4f6b-98ef-e0780d62854b', 6, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (12, 'urn:musclecuties:seed:v1:0c2e1b05-eddb-4434-afa1-f4a5947f0614', 6, 2, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (13, 'urn:musclecuties:seed:v1:d84f6997-2dcb-4e47-8beb-d3001379619e', 6, 7, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (14, 'urn:musclecuties:seed:v1:ccefbc28-b6aa-4f22-89ae-b7d682a3547d', 7, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (15, 'urn:musclecuties:seed:v1:4bea1661-4490-46bf-be48-83247ae5402f', 8, 2, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (16, 'urn:musclecuties:seed:v1:1d04ce28-4735-4cd4-9dde-386955806cba', 8, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (17, 'urn:musclecuties:seed:v1:01efd6da-5baa-4798-bee3-468379969fd7', 8, 7, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (18, 'urn:musclecuties:seed:v1:a9c2faf9-e798-4681-bbea-9fdf98b52f7f', 8, 12, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (19, 'urn:musclecuties:seed:v1:7386a60f-2899-4af3-855c-9c5bf329bbbc', 9, 2, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (20, 'urn:musclecuties:seed:v1:d60cfe7f-8c45-4624-b465-1177220a1b15', 9, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (21, 'urn:musclecuties:seed:v1:4ef403ee-e6d4-4a19-a4e3-b51a36f181f4', 9, 7, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (22, 'urn:musclecuties:seed:v1:2941bf31-bc59-4f07-a6b7-75753ce908a8', 9, 12, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (23, 'urn:musclecuties:seed:v1:8632a721-eb1c-4b16-abd7-394e26467447', 10, 2, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (24, 'urn:musclecuties:seed:v1:261867ad-a133-4f16-bbca-b7a60c5e0fec', 10, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (25, 'urn:musclecuties:seed:v1:accc575e-d33f-4c89-a089-51b3c1fb6888', 10, 7, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (26, 'urn:musclecuties:seed:v1:7b3bcfc0-ef76-4dbe-876e-31de871153c1', 11, 2, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (27, 'urn:musclecuties:seed:v1:a19ce2c1-3015-4f8d-953a-1fed93b71f02', 11, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (28, 'urn:musclecuties:seed:v1:60e0c7bc-4be1-432e-b54e-e1f7844835bb', 11, 3, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (29, 'urn:musclecuties:seed:v1:4ba7cc39-8058-410f-aac9-5f09a9d75533', 11, 12, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (30, 'urn:musclecuties:seed:v1:c0ced038-dabd-49a7-9746-d3062559c82d', 12, 2, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (31, 'urn:musclecuties:seed:v1:db74a5f9-b6c6-4277-b2ef-de7b1c3d85f5', 13, 2, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (32, 'urn:musclecuties:seed:v1:5cae3bfe-680f-4c59-bdf9-044bc4ff071a', 14, 2, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (33, 'urn:musclecuties:seed:v1:400fbea7-3cd2-4186-b000-347769ebdf17', 15, 2, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (34, 'urn:musclecuties:seed:v1:48730b7a-b852-4518-8b7e-257a7561ad4a', 15, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (35, 'urn:musclecuties:seed:v1:78ffd417-6d45-45fb-b8b6-0971b516b672', 15, 7, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (36, 'urn:musclecuties:seed:v1:852cafe7-56b7-4819-b2ca-049a3174ffaf', 16, 5, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (37, 'urn:musclecuties:seed:v1:5fc3bd91-4327-47cb-b2a6-19627a72e23d', 16, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (38, 'urn:musclecuties:seed:v1:3ae93449-ca27-48a9-9e5e-7a3b81c326b7', 16, 4, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (39, 'urn:musclecuties:seed:v1:64e88bd6-c5cb-4418-87e9-14ef2d709bea', 16, 12, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (40, 'urn:musclecuties:seed:v1:879e07d0-948d-4bf9-a9ca-bc8b3e8a7ff6', 17, 5, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (41, 'urn:musclecuties:seed:v1:91c9d848-a83b-465c-87ea-4cbabe3cdf87', 17, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (42, 'urn:musclecuties:seed:v1:afbccf47-b372-436f-821d-fd083a1774e1', 18, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (43, 'urn:musclecuties:seed:v1:69e59a73-2536-4b08-9826-25a8721170e6', 18, 5, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (44, 'urn:musclecuties:seed:v1:aab0f01b-2a29-4ea0-8f17-cb1e11e6f027', 18, 4, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (45, 'urn:musclecuties:seed:v1:d0f0b8c8-c8e3-4e26-becc-589115a8d2eb', 19, 5, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (46, 'urn:musclecuties:seed:v1:bf299d4f-23bd-4da7-b581-a383ca3218e5', 19, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (47, 'urn:musclecuties:seed:v1:00b14dd0-2d34-46fb-9077-7945a7ecca26', 19, 12, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (48, 'urn:musclecuties:seed:v1:bc85387b-7779-4908-8adc-c93fee7bdd7d', 20, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (49, 'urn:musclecuties:seed:v1:1f1a5030-66b6-4e98-934f-40853bea76ba', 20, 5, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (50, 'urn:musclecuties:seed:v1:7267028d-a8a7-4ae2-8e20-f6e20603f665', 20, 2, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (51, 'urn:musclecuties:seed:v1:6f98e20c-4aa4-4a08-943d-c0ccb84b5727', 20, 3, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (52, 'urn:musclecuties:seed:v1:25d87e2c-3bfa-4384-9b77-fbbe8949f175', 21, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (53, 'urn:musclecuties:seed:v1:b5073d7c-6374-40e3-a626-2b0833a192bb', 21, 5, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (54, 'urn:musclecuties:seed:v1:06f49724-3b98-4b88-891b-83907e25665f', 21, 3, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (55, 'urn:musclecuties:seed:v1:f1725d78-4c24-43ae-ba84-eeed800b820d', 22, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (56, 'urn:musclecuties:seed:v1:1992a3b0-6e6b-455c-a579-ea7d36137089', 22, 5, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (57, 'urn:musclecuties:seed:v1:07b6a564-de26-4677-8443-a149ea80c471', 22, 3, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (58, 'urn:musclecuties:seed:v1:7e9a50c7-8d41-4059-9ade-89242613a6bb', 23, 1, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (59, 'urn:musclecuties:seed:v1:4cc87bd4-776e-4401-8b7d-b7e91565cdf7', 23, 5, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (60, 'urn:musclecuties:seed:v1:cc68d418-9436-4830-a0f6-60116a5ebb95', 23, 3, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (61, 'urn:musclecuties:seed:v1:df824605-9867-4bcc-b172-bef9a7771ca9', 24, 3, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (62, 'urn:musclecuties:seed:v1:99709741-f442-421f-9216-da1429a7ebe7', 24, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (63, 'urn:musclecuties:seed:v1:6f212d4d-ca4c-49e1-ba4a-28e9c187bfc4', 25, 3, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (64, 'urn:musclecuties:seed:v1:be3f0caf-f8e3-469e-a2d0-fd105240083f', 25, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (65, 'urn:musclecuties:seed:v1:6e417441-b497-470e-92e8-8ed5d8f05615', 26, 3, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (66, 'urn:musclecuties:seed:v1:cc0798f3-3f2e-4697-8acd-0abf101281af', 26, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (67, 'urn:musclecuties:seed:v1:c53a660f-b607-4dd8-955d-0c6759d1b399', 27, 3, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (68, 'urn:musclecuties:seed:v1:e3e9ad03-5186-4c0f-80ef-a18cd9a8e6b3', 27, 1, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (69, 'urn:musclecuties:seed:v1:74a8e306-a469-4417-b388-eb56c3ce16ba', 28, 3, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (70, 'urn:musclecuties:seed:v1:2abe4393-6a74-4867-b4fa-8cc97d798d65', 29, 4, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (71, 'urn:musclecuties:seed:v1:84a55e7f-a1a3-449e-95b7-40dd11d0a9d8', 29, 12, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (72, 'urn:musclecuties:seed:v1:db3de9ae-44e1-4449-b36a-0f0f0cb0dbc1', 30, 4, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (73, 'urn:musclecuties:seed:v1:0b74281a-ae16-4294-ad45-34b1f7276a91', 31, 6, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (74, 'urn:musclecuties:seed:v1:d7df8a82-fb8a-4d7d-83c6-8e3fc03fb115', 31, 10, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (75, 'urn:musclecuties:seed:v1:cda7e213-5fa1-421e-91a7-f47f54c36991', 31, 12, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (76, 'urn:musclecuties:seed:v1:317d15bc-3dbc-42d7-8ca6-487e5a8269d3', 32, 6, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (77, 'urn:musclecuties:seed:v1:c5dbb5d0-eb8d-4a79-8c4c-705483cec6f0', 32, 10, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (78, 'urn:musclecuties:seed:v1:1030971d-846b-4e2b-9d4e-8d27eecc9826', 33, 7, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (79, 'urn:musclecuties:seed:v1:1fd920d6-c1fd-40ae-a21d-d69cb6931603', 33, 6, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (80, 'urn:musclecuties:seed:v1:48c07286-fb62-4944-a5fe-4a4e9685e7f8', 33, 10, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (81, 'urn:musclecuties:seed:v1:dbdc8fa5-36bf-4d12-90f8-aa5c081dd63a', 34, 7, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (82, 'urn:musclecuties:seed:v1:4ece6d5a-cbb0-469f-b429-518a6d055b95', 34, 6, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (83, 'urn:musclecuties:seed:v1:0d49bf1b-b9ef-4261-b7c4-cc0317225dfe', 34, 10, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (84, 'urn:musclecuties:seed:v1:7a598d65-c4b0-4640-8b44-533c0066ec8f', 35, 7, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (85, 'urn:musclecuties:seed:v1:358a2109-a2de-4dc3-92dd-6001a49da9e2', 35, 6, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (86, 'urn:musclecuties:seed:v1:c5237261-39b0-4653-8e2f-bc0bfe7e0564', 35, 10, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (87, 'urn:musclecuties:seed:v1:c4d76553-49c8-4514-a993-91be3a282a70', 36, 6, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (88, 'urn:musclecuties:seed:v1:d0b6b2cc-e63f-445b-ac7a-9b20d55898ff', 37, 8, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (89, 'urn:musclecuties:seed:v1:3100faff-6072-4181-9023-4ed8703037be', 37, 7, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (90, 'urn:musclecuties:seed:v1:f7132da7-f84b-4928-befc-ab95123e8624', 38, 8, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (91, 'urn:musclecuties:seed:v1:9676ea3d-3d65-44a5-accc-9c9867fe01f9', 38, 11, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (92, 'urn:musclecuties:seed:v1:b87fb679-2883-4ba6-b397-422d4d63cb8c', 39, 8, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (93, 'urn:musclecuties:seed:v1:6b4eec17-30c6-48ea-bc86-b8bf659353bf', 39, 11, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (94, 'urn:musclecuties:seed:v1:670c250b-22ad-42d3-b7bb-f3852357c193', 40, 9, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (95, 'urn:musclecuties:seed:v1:f3be2a86-c42c-46f7-b228-144db39d44b2', 40, 8, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (96, 'urn:musclecuties:seed:v1:afe115a3-376f-42bd-9db8-72bfd1f2999d', 40, 11, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (97, 'urn:musclecuties:seed:v1:21e6d739-3554-456c-9776-b472109b02e4', 41, 9, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (98, 'urn:musclecuties:seed:v1:8209c302-dae1-4bd4-b7f5-4e678e9b05de', 41, 8, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (99, 'urn:musclecuties:seed:v1:aa78c491-a908-463c-a68d-b50bbb2719a2', 41, 11, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (100, 'urn:musclecuties:seed:v1:79d75b6b-0bdf-42e2-b267-597bb35da738', 42, 9, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (101, 'urn:musclecuties:seed:v1:e87efa3f-4d6b-42d1-a26b-ab2b94da2ad1', 42, 11, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (102, 'urn:musclecuties:seed:v1:1cc46149-afe9-42f0-98ff-a41c8fbd62b7', 42, 12, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (103, 'urn:musclecuties:seed:v1:92284c9f-1f5e-4d9c-a90c-57d7749e6e21', 43, 8, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (104, 'urn:musclecuties:seed:v1:d231267b-e593-43b9-9f5f-e487dac46a0a', 44, 8, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (105, 'urn:musclecuties:seed:v1:36901794-5b21-459f-8455-fea4a7817d89', 45, 8, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (106, 'urn:musclecuties:seed:v1:b989c9cc-0e89-440d-bb11-0da96452ce81', 45, 7, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (107, 'urn:musclecuties:seed:v1:6d4b871a-af4b-4e87-ab09-812ebd3f69a1', 46, 10, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (108, 'urn:musclecuties:seed:v1:ea514b71-7dae-4c46-a595-fda2a2d91994', 47, 10, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (109, 'urn:musclecuties:seed:v1:34b2f991-f0ea-4751-8168-6a8754f48e62', 48, 11, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (110, 'urn:musclecuties:seed:v1:be94313b-f436-496c-b207-1763641a572b', 49, 11, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (111, 'urn:musclecuties:seed:v1:ea220668-3224-4b4c-b264-8ae6024e914a', 50, 12, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (112, 'urn:musclecuties:seed:v1:51c2cd87-fad9-414b-b0c5-979565b04f9e', 51, 12, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (113, 'urn:musclecuties:seed:v1:a61f0b78-ed74-409e-b1a7-88d0e3dd6e5e', 51, 6, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (114, 'urn:musclecuties:seed:v1:c56cda32-9e7a-46c9-a61e-bc841828fdbe', 52, 12, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (115, 'urn:musclecuties:seed:v1:777515b7-782c-473d-9fd9-772058fd18e9', 53, 12, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (116, 'urn:musclecuties:seed:v1:962ed9b3-52eb-44fe-bd69-e9897ff78e95', 54, 12, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (117, 'urn:musclecuties:seed:v1:433a3000-4f5f-4b51-9234-e9549b3ebd1f', 55, 12, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (118, 'urn:musclecuties:seed:v1:a1d0c259-0414-4b55-ac68-e4e7087268d5', 56, 12, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (119, 'urn:musclecuties:seed:v1:c2c27cfa-992a-4baa-b629-9ed13e7eab1b', 57, 12, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (120, 'urn:musclecuties:seed:v1:a5b1c6a8-29af-4e44-b5c0-f084bfe7ab4b', 57, 7, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (121, 'urn:musclecuties:seed:v1:4273edfb-3cd2-4c3e-820f-93018d800f07', 58, 13, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (122, 'urn:musclecuties:seed:v1:28cd9ef6-58fd-4cde-848b-44f297c1b672', 59, 13, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (123, 'urn:musclecuties:seed:v1:c284a020-9ead-46d9-bc87-4076744d9ed6', 60, 6, 1.0);
INSERT INTO catalog.exercise_muscle_contributions VALUES (124, 'urn:musclecuties:seed:v1:3c577099-c304-4b87-add5-8ec717b7aee0', 60, 7, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (125, 'urn:musclecuties:seed:v1:e6f759af-18e6-4bb7-9396-599ede34a24d', 60, 10, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (126, 'urn:musclecuties:seed:v1:f0c6a99e-d57d-4275-a0b9-e94fc407c3ae', 60, 12, 0.5);
INSERT INTO catalog.exercise_muscle_contributions VALUES (127, 'urn:musclecuties:seed:v1:87da5477-6249-4d2e-9ccb-d7ed9b6b86b2', 60, 5, 0.5);


--
-- Data for Name: exercises; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.exercises VALUES (1, 'urn:musclecuties:seed:v1:127973ac-1dab-40ab-8d60-28b0d5250ed7', 'ENGINE_101', 'Barbell Hip Thrust', 'A heavy hip-extension movement for progressive glute strength.', NULL, NULL, NULL, 'Glutes', 'Hamstrings,Abs', '', true);
INSERT INTO catalog.exercises VALUES (2, 'urn:musclecuties:seed:v1:b716db3e-0de4-4d6c-abdc-6e4852fce123', 'ENGINE_102', 'Machine Hip Thrust', 'Hip extension focused on glute strength and control.', NULL, NULL, 'Keep the ribs stacked, drive through the feet, and finish with the glutes instead of the lower back.', 'Glutes', 'Hamstrings', '', true);
INSERT INTO catalog.exercises VALUES (3, 'urn:musclecuties:seed:v1:6e118968-15b0-409f-8ce9-351be91349be', 'ENGINE_103', 'B-Stance Hip Thrust', 'Hip extension focused on glute strength and control.', NULL, NULL, 'Keep the ribs stacked, drive through the feet, and finish with the glutes instead of the lower back.', 'Glutes', 'Hamstrings', '', true);
INSERT INTO catalog.exercises VALUES (4, 'urn:musclecuties:seed:v1:107daad1-a02a-4bbe-8c82-67c74564fb47', 'ENGINE_104', 'Single-Leg Glute Bridge', 'Hip extension focused on glute strength and control.', NULL, NULL, 'Keep the ribs stacked, drive through the feet, and finish with the glutes instead of the lower back.', 'Glutes', 'Hamstrings', '', true);
INSERT INTO catalog.exercises VALUES (5, 'urn:musclecuties:seed:v1:57c431fb-8560-4154-b850-edbae9487b5b', 'ENGINE_105', 'Cable Pull-Through', 'A cable hinge pattern that builds glutes and hamstrings with lower spinal demand.', NULL, NULL, NULL, 'Glutes', 'Hamstrings,LowerBack', '', true);
INSERT INTO catalog.exercises VALUES (6, 'urn:musclecuties:seed:v1:06144e91-4cb4-4347-b212-b0da218622c0', 'ENGINE_106', '45 Degree Back Extension', 'A hip hinge that trains the posterior chain through a controlled range.', NULL, NULL, 'Push the hips back, keep the load close, and stop when the hamstrings limit the range.', 'Glutes', 'Hamstrings,UpperBack', 'LowerBack', false);
INSERT INTO catalog.exercises VALUES (7, 'urn:musclecuties:seed:v1:c4747779-a0ca-40d8-95fd-4291a49ff53e', 'ENGINE_107', 'Kneeling Cable Kickback', 'Hip extension focused on glute strength and control.', NULL, NULL, 'Keep the ribs stacked, drive through the feet, and finish with the glutes instead of the lower back.', 'Glutes', '', 'Knee', false);
INSERT INTO catalog.exercises VALUES (8, 'urn:musclecuties:seed:v1:9c5c7a0c-de19-44e7-9daa-217b6f8e8677', 'ENGINE_111', 'Romanian Deadlift', 'A hip-hinge movement for hamstrings, glutes, and posterior-chain control.', NULL, NULL, NULL, 'Hamstrings', 'Glutes,LowerBack', 'LowerBack', false);
INSERT INTO catalog.exercises VALUES (9, 'urn:musclecuties:seed:v1:30047258-225e-4292-a780-773383a34535', 'ENGINE_112', 'Deficit RDL', 'A hip hinge that trains the posterior chain through a controlled range.', NULL, NULL, 'Push the hips back, keep the load close, and stop when the hamstrings limit the range.', 'Hamstrings', 'Glutes,UpperBack,Core', 'LowerBack', false);
INSERT INTO catalog.exercises VALUES (10, 'urn:musclecuties:seed:v1:25b85e9d-b19c-4f79-857c-c11ce19d8c09', 'ENGINE_113', 'Dumbbell RDL', 'A hip hinge that trains the posterior chain through a controlled range.', NULL, NULL, 'Push the hips back, keep the load close, and stop when the hamstrings limit the range.', 'Hamstrings', 'Glutes,UpperBack', 'LowerBack', false);
INSERT INTO catalog.exercises VALUES (11, 'urn:musclecuties:seed:v1:8ca77495-399b-4235-b7e2-051dc465be77', 'ENGINE_114', 'Single-Leg RDL', 'A hip hinge that trains the posterior chain through a controlled range.', NULL, NULL, 'Push the hips back, keep the load close, and stop when the hamstrings limit the range.', 'Hamstrings', 'Glutes,GluteMed,Core', 'Ankle', false);
INSERT INTO catalog.exercises VALUES (12, 'urn:musclecuties:seed:v1:f0af86d7-f7f7-454f-91db-fa89c29caf60', 'ENGINE_115', 'Seated Leg Curl', 'A hamstring isolation movement for knee-flexion strength and posterior-leg balance.', NULL, NULL, NULL, 'Hamstrings', '', 'Knee', false);
INSERT INTO catalog.exercises VALUES (13, 'urn:musclecuties:seed:v1:fd67734e-626d-41bc-b07a-f9a7f7d8c29d', 'ENGINE_116', 'Lying Leg Curl', 'Direct hamstring work through knee flexion.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'Hamstrings', '', '', true);
INSERT INTO catalog.exercises VALUES (14, 'urn:musclecuties:seed:v1:82775a26-937b-4360-858b-afcb97d07d6e', 'ENGINE_117', 'Nordic Curl', 'Direct hamstring work through knee flexion.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'Hamstrings', '', 'Knee', false);
INSERT INTO catalog.exercises VALUES (15, 'urn:musclecuties:seed:v1:facd4799-a7dc-458b-b7b3-38896ba1a39f', 'ENGINE_118', 'Good Morning', 'A hip hinge that trains the posterior chain through a controlled range.', NULL, NULL, 'Push the hips back, keep the load close, and stop when the hamstrings limit the range.', 'Hamstrings', 'Glutes,UpperBack', 'LowerBack', false);
INSERT INTO catalog.exercises VALUES (16, 'urn:musclecuties:seed:v1:6d200ce2-06ce-4cd3-a280-b321bc8af646', 'ENGINE_121', 'Back Squat', 'A squat pattern for quads, glutes, and bracing strength.', NULL, NULL, 'Brace before descending, keep the whole foot planted, and let the knees track with the toes.', 'Glutes', 'Quads,Adductors,Core', 'Knee,LowerBack', false);
INSERT INTO catalog.exercises VALUES (17, 'urn:musclecuties:seed:v1:8180cfa2-ad1b-4812-85d7-6f572ebd76fc', 'ENGINE_122', 'Hack Squat', 'A squat pattern for quads, glutes, and bracing strength.', NULL, NULL, 'Brace before descending, keep the whole foot planted, and let the knees track with the toes.', 'Quads', 'Glutes', 'Knee', false);
INSERT INTO catalog.exercises VALUES (18, 'urn:musclecuties:seed:v1:96c7ba90-c27c-44df-a969-bc8e9641f5ab', 'ENGINE_123', 'Leg Press', 'A stable lower-body strength movement that lets quads and glutes take more volume.', NULL, NULL, NULL, 'Quads', 'Glutes,Hamstrings', 'Knee', false);
INSERT INTO catalog.exercises VALUES (19, 'urn:musclecuties:seed:v1:276ef30f-c78c-4d4b-b756-014f223b70dd', 'ENGINE_124', 'Goblet Squat', 'A controlled squat pattern for quads, glutes, and full-body bracing.', NULL, NULL, NULL, 'Quads', 'Glutes,Hamstrings', '', true);
INSERT INTO catalog.exercises VALUES (20, 'urn:musclecuties:seed:v1:263b9e76-0f7e-437f-939d-401d4c71e058', 'ENGINE_125', 'Bulgarian Split Squat', 'A high-return single-leg squat for glutes, quads, and balance.', NULL, NULL, NULL, 'Glutes', 'Quads,Hamstrings', 'Knee,Hip', false);
INSERT INTO catalog.exercises VALUES (21, 'urn:musclecuties:seed:v1:c769c573-c74b-4014-bd97-340ffacfade9', 'ENGINE_126', 'Walking Lunge', 'A dynamic single-leg pattern for glutes, quads, conditioning, and coordination.', NULL, NULL, NULL, 'Quads', 'Glutes,Hamstrings', 'Knee', false);
INSERT INTO catalog.exercises VALUES (22, 'urn:musclecuties:seed:v1:e4de634e-1c75-418a-87b7-9a7b09654e42', 'ENGINE_127', 'Step-Up', 'A single-leg lower-body exercise for glutes, quads, and balance.', NULL, NULL, NULL, 'Glutes', 'Quads,Calves', '', true);
INSERT INTO catalog.exercises VALUES (23, 'urn:musclecuties:seed:v1:25ecfd18-4268-4dc8-9ca5-9ff9ae29c378', 'ENGINE_128', 'Reverse Lunge', 'A single-leg strength movement with controlled knee and hip loading.', NULL, NULL, NULL, 'Quads', 'Glutes,Hamstrings', 'Knee', false);
INSERT INTO catalog.exercises VALUES (24, 'urn:musclecuties:seed:v1:66a90ab6-96d3-4c83-ac94-f8fb6ec9297a', 'ENGINE_131', 'Seated Hip Abduction Lean', 'Hip abduction for lateral glute strength and pelvic control.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'GluteMed', 'Glutes', '', true);
INSERT INTO catalog.exercises VALUES (25, 'urn:musclecuties:seed:v1:a97be7c8-ab06-4081-9c7f-79cdeda13874', 'ENGINE_132', 'Seated Hip Abduction Upright', 'Hip abduction for lateral glute strength and pelvic control.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'GluteMed', 'Glutes', '', true);
INSERT INTO catalog.exercises VALUES (26, 'urn:musclecuties:seed:v1:fc3d80c3-67ed-4817-a655-72f0c06228ab', 'ENGINE_133', 'Standing Cable Abduction', 'Hip abduction for lateral glute strength and pelvic control.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'GluteMed', 'Glutes', '', true);
INSERT INTO catalog.exercises VALUES (27, 'urn:musclecuties:seed:v1:2238d7c2-85da-4223-bee2-87f5798c8f4b', 'ENGINE_134', 'Banded Lateral Walk', 'Hip abduction for lateral glute strength and pelvic control.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'GluteMed', 'Glutes', '', true);
INSERT INTO catalog.exercises VALUES (28, 'urn:musclecuties:seed:v1:312a49ec-235f-4797-a2b5-d28671eb4e51', 'ENGINE_135', 'Side-Lying Abduction', 'Hip abduction for lateral glute strength and pelvic control.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'GluteMed', '', '', true);
INSERT INTO catalog.exercises VALUES (29, 'urn:musclecuties:seed:v1:6b0196fc-b68e-438c-aa6e-b768ea72e17b', 'ENGINE_136', 'Copenhagen Plank', 'Adductor strength for hip stability and lower-body balance.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'Adductors', 'Core', '', false);
INSERT INTO catalog.exercises VALUES (30, 'urn:musclecuties:seed:v1:55d4b4d6-ff24-4651-9e41-265ab27e87fe', 'ENGINE_137', 'Seated Adduction Machine', 'Adductor strength for hip stability and lower-body balance.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'Adductors', '', '', true);
INSERT INTO catalog.exercises VALUES (31, 'urn:musclecuties:seed:v1:18361eaa-b780-42bb-b036-8ec35c273613', 'ENGINE_141', 'Pull-Up', 'Vertical pulling for lats, upper back, and arm strength.', NULL, NULL, 'Set the shoulder blades, pull with the elbows, and control the return.', 'Lats', 'Biceps,Core', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (32, 'urn:musclecuties:seed:v1:83ab7492-fc32-4e07-a772-86c0c8d3d44e', 'ENGINE_142', 'Lat Pulldown', 'A vertical pulling exercise for back strength and posture support.', NULL, NULL, NULL, 'UpperBack', 'Biceps,RearShoulders', '', true);
INSERT INTO catalog.exercises VALUES (33, 'urn:musclecuties:seed:v1:4ffae5a9-f456-4c60-9ff3-44adab7eaf6c', 'ENGINE_143', 'Chest Supported Row', 'A strict horizontal pull for upper-back thickness without lower-back cheating.', NULL, NULL, NULL, 'UpperBack', 'Biceps,RearShoulders', '', true);
INSERT INTO catalog.exercises VALUES (34, 'urn:musclecuties:seed:v1:35febbb4-ccd0-4cf6-9f3c-ef5dffa27d5c', 'ENGINE_144', 'Seated Cable Row', 'A controlled horizontal pull for upper-back strength.', NULL, NULL, NULL, 'UpperBack', 'Biceps,RearShoulders', '', true);
INSERT INTO catalog.exercises VALUES (35, 'urn:musclecuties:seed:v1:8983dd39-d3f8-4c2c-9244-6ec31fe95bec', 'ENGINE_145', 'Single-Arm DB Row', 'Horizontal pulling for upper-back strength and shoulder control.', NULL, NULL, 'Set the shoulder blades, pull with the elbows, and control the return.', 'UpperBack', 'Lats,Biceps', 'LowerBack', false);
INSERT INTO catalog.exercises VALUES (36, 'urn:musclecuties:seed:v1:1b29e734-7a60-4a29-a441-edd94588b1b1', 'ENGINE_146', 'Straight-Arm Pulldown', 'Vertical pulling for lats, upper back, and arm strength.', NULL, NULL, 'Set the shoulder blades, pull with the elbows, and control the return.', 'Lats', '', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (37, 'urn:musclecuties:seed:v1:2968b2ab-7388-4fc7-84c1-3f6e9a5ce349', 'ENGINE_147', 'Face Pull', 'A rear-shoulder and upper-back accessory for posture and shoulder balance.', NULL, NULL, NULL, 'RearShoulders', 'UpperBack', '', true);
INSERT INTO catalog.exercises VALUES (38, 'urn:musclecuties:seed:v1:67de3c98-4129-49aa-b97d-5687ebe0df8c', 'ENGINE_151', 'DB Shoulder Press', 'Overhead pressing for shoulder and triceps strength.', NULL, NULL, 'Keep the torso stable, use a controlled lowering phase, and avoid forcing painful range.', 'SideShoulders', 'Triceps', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (39, 'urn:musclecuties:seed:v1:8b3cef53-be66-4841-831e-ed09db2ea978', 'ENGINE_152', 'Machine Shoulder Press', 'Overhead pressing for shoulder and triceps strength.', NULL, NULL, 'Keep the torso stable, use a controlled lowering phase, and avoid forcing painful range.', 'SideShoulders', 'Triceps', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (40, 'urn:musclecuties:seed:v1:5ec06b28-4f69-4c55-a959-1f69a4323690', 'ENGINE_153', 'Incline DB Press', 'Pressing work for chest, shoulders, and triceps.', NULL, NULL, 'Keep the torso stable, use a controlled lowering phase, and avoid forcing painful range.', 'Chest', 'SideShoulders,Triceps', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (41, 'urn:musclecuties:seed:v1:9116249f-c7f8-4f08-aa04-f6077c41024e', 'ENGINE_154', 'Machine Chest Press', 'Pressing work for chest, shoulders, and triceps.', NULL, NULL, 'Keep the torso stable, use a controlled lowering phase, and avoid forcing painful range.', 'Chest', 'SideShoulders,Triceps', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (42, 'urn:musclecuties:seed:v1:8eda79f3-8537-45ff-9a15-3ac37c3e5f03', 'ENGINE_155', 'Push-Up', 'Pressing work for chest, shoulders, and triceps.', NULL, NULL, 'Keep the torso stable, use a controlled lowering phase, and avoid forcing painful range.', 'Chest', 'Triceps,Core', 'Shoulder,Wrist', false);
INSERT INTO catalog.exercises VALUES (43, 'urn:musclecuties:seed:v1:240891e2-f5aa-481e-b83b-f740cf0f20bc', 'ENGINE_156', 'Cable Lateral Raise', 'A constant-tension side-delt accessory for shoulder shape.', NULL, NULL, NULL, 'SideShoulders', '', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (44, 'urn:musclecuties:seed:v1:2b685cd9-6da2-466e-ae6e-29d1af1346c9', 'ENGINE_157', 'DB Lateral Raise', 'Focused lateral shoulder work with low systemic fatigue.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'SideShoulders', '', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (45, 'urn:musclecuties:seed:v1:308fa490-907d-40ea-89b7-b1a085beae58', 'ENGINE_158', 'Reverse Pec Deck', 'Rear-shoulder work for balanced pressing and pulling mechanics.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'SideShoulders', 'UpperBack', '', true);
INSERT INTO catalog.exercises VALUES (46, 'urn:musclecuties:seed:v1:36ad213b-86f4-49e1-ba67-ba2b665ea027', 'ENGINE_161', 'Incline DB Curl', 'Direct biceps work through controlled elbow flexion.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'Biceps', '', '', true);
INSERT INTO catalog.exercises VALUES (47, 'urn:musclecuties:seed:v1:acb00515-3885-4c58-bc84-4341cfbd5a27', 'ENGINE_162', 'Cable Curl', 'Direct biceps work through controlled elbow flexion.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'Biceps', '', '', true);
INSERT INTO catalog.exercises VALUES (48, 'urn:musclecuties:seed:v1:ccd1d731-da51-446f-9097-9fda900f076d', 'ENGINE_163', 'Overhead Cable Extension', 'Direct triceps work through controlled elbow extension.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'Triceps', '', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (49, 'urn:musclecuties:seed:v1:21e90a50-9f03-41bd-8052-b7f4139d1cfd', 'ENGINE_164', 'Rope Pushdown', 'Direct triceps work through controlled elbow extension.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'Triceps', '', '', true);
INSERT INTO catalog.exercises VALUES (50, 'urn:musclecuties:seed:v1:59160d9d-b75d-4638-bc73-61df544591e8', 'ENGINE_171', 'Dead Bug', 'A low-impact core stability drill.', NULL, NULL, NULL, 'Abs', 'HipFlexors', '', true);
INSERT INTO catalog.exercises VALUES (51, 'urn:musclecuties:seed:v1:274e93bf-31fa-4e10-a69c-c0cbfdcd19bb', 'ENGINE_172', 'Ab Wheel Rollout', 'Core training that resists excessive lower-back extension.', NULL, NULL, 'Brace while breathing normally and stop before the spine loses position.', 'Core', 'Lats', 'Shoulder,LowerBack', false);
INSERT INTO catalog.exercises VALUES (52, 'urn:musclecuties:seed:v1:2be467a0-f8be-442c-80bf-088765a2efce', 'ENGINE_173', 'Hollow Hold', 'Core training that resists excessive lower-back extension.', NULL, NULL, 'Brace while breathing normally and stop before the spine loses position.', 'Core', '', '', false);
INSERT INTO catalog.exercises VALUES (53, 'urn:musclecuties:seed:v1:b60d68e0-01df-42f3-9337-4e208e3865db', 'ENGINE_174', 'Cable Crunch', 'Core training that resists excessive lower-back extension.', NULL, NULL, 'Brace while breathing normally and stop before the spine loses position.', 'Core', '', '', false);
INSERT INTO catalog.exercises VALUES (54, 'urn:musclecuties:seed:v1:4a468a3a-b3a4-4b72-a923-686767d7ce35', 'ENGINE_175', 'Pallof Press', 'An anti-rotation core exercise for trunk stability.', NULL, NULL, NULL, 'Obliques', 'Abs', '', true);
INSERT INTO catalog.exercises VALUES (55, 'urn:musclecuties:seed:v1:909277f0-7312-405c-a8b8-73d3839988f6', 'ENGINE_176', 'Bird Dog', 'A low-impact core and hip stability drill.', NULL, NULL, NULL, 'Abs', 'Glutes,LowerBack', '', true);
INSERT INTO catalog.exercises VALUES (56, 'urn:musclecuties:seed:v1:b79d0440-3ba7-4156-82e1-a10c63da69a5', 'ENGINE_177', 'Side Plank', 'A lateral core stability exercise.', NULL, NULL, NULL, 'Obliques', 'Abs,SideShoulders', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (57, 'urn:musclecuties:seed:v1:10a50362-617e-4471-8d38-91910aaff6cf', 'ENGINE_178', 'Suitcase Carry', 'Core training that resists side bending.', NULL, NULL, 'Brace while breathing normally and stop before the spine loses position.', 'Core', 'UpperBack', '', true);
INSERT INTO catalog.exercises VALUES (58, 'urn:musclecuties:seed:v1:208977f4-b4e8-4d57-8a03-7521c22d673f', 'ENGINE_181', 'Standing Calf Raise', 'Calf strength through a full ankle range of motion.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'Calves', '', 'Ankle', false);
INSERT INTO catalog.exercises VALUES (59, 'urn:musclecuties:seed:v1:f8f56f1f-4fc3-4b26-996d-d3561e906f39', 'ENGINE_182', 'Seated Calf Raise', 'Calf strength through a full ankle range of motion.', NULL, NULL, 'Use a controlled range, keep the target joint aligned, and stop if the movement causes sharp pain.', 'Calves', '', 'Ankle', false);
INSERT INTO catalog.exercises VALUES (60, 'urn:musclecuties:seed:v1:327ab4fa-1d88-4fd9-ad82-d0ce20b1d4f4', 'ENGINE_191', 'Rock Climbing', 'A skill-based climbing session for grip, pulling strength, and body tension.', NULL, NULL, NULL, 'UpperBack', 'Biceps,Forearms,Abs', 'Shoulder,Wrist,Elbow', false);
INSERT INTO catalog.exercises VALUES (61, 'urn:musclecuties:seed:v1:851e22dc-4755-4fa6-b72b-27219cf41709', 'HIP_THRUST', 'Hip Thrust', 'A hip-extension strength movement focused on glutes.', NULL, NULL, NULL, 'Glutes', 'Hamstrings,Abs', '', true);
INSERT INTO catalog.exercises VALUES (62, 'urn:musclecuties:seed:v1:cb7cf41a-c838-4ce1-829d-ad627ed7715f', 'SINGLE_LEG_ROMANIAN_DEADLIFT', 'Single-Leg Romanian Deadlift', 'A single-leg hinge for hamstrings, glutes, balance, and hip control.', NULL, NULL, NULL, 'Hamstrings', 'Glutes,LowerBack', 'LowerBack,Hip', false);
INSERT INTO catalog.exercises VALUES (63, 'urn:musclecuties:seed:v1:74a1b1a9-926f-4679-b79a-32d59fa1cbbb', 'LEG_EXTENSION', 'Leg Extension', 'A focused quad accessory for extra thigh volume without taxing the whole body.', NULL, NULL, NULL, 'Quads', '', 'Knee', false);
INSERT INTO catalog.exercises VALUES (64, 'urn:musclecuties:seed:v1:076f599f-5304-4b6e-9080-bf33d2af8562', 'GLUTE_BRIDGE', 'Glute Bridge', 'A low-impact glute exercise that works well for lighter cycle phases.', NULL, NULL, NULL, 'Glutes', 'Hamstrings,Abs', '', true);
INSERT INTO catalog.exercises VALUES (65, 'urn:musclecuties:seed:v1:fcb6c394-3556-41d0-9a5b-4f69fee4c87c', 'CABLE_GLUTE_KICKBACK', 'Cable Glute Kickback', 'A glute accessory that trains hip extension with a long squeeze and low systemic fatigue.', NULL, NULL, NULL, 'Glutes', 'Hamstrings', '', true);
INSERT INTO catalog.exercises VALUES (66, 'urn:musclecuties:seed:v1:9629bd99-3d2b-4576-aa53-555d32ee72ee', 'CABLE_HIP_ABDUCTION', 'Cable Hip Abduction', 'A glute medius accessory for hip shape, pelvic control, and single-leg stability.', NULL, NULL, NULL, 'Glutes', 'HipFlexors', '', true);
INSERT INTO catalog.exercises VALUES (67, 'urn:musclecuties:seed:v1:b90bad9f-5db9-40b4-acc5-a617c87f71c9', 'BACK_EXTENSION', 'Back Extension', 'A posterior-chain accessory that can bias glutes, hamstrings, or lower back by setup.', NULL, NULL, NULL, 'Glutes', 'Hamstrings,LowerBack', 'LowerBack', false);
INSERT INTO catalog.exercises VALUES (68, 'urn:musclecuties:seed:v1:2b0efdb6-d83e-4040-8728-f0e3d2ca0ac6', 'CALF_RAISE', 'Calf Raise', 'A simple calf-strength movement for lower-leg support.', NULL, NULL, NULL, 'Calves', '', '', true);
INSERT INTO catalog.exercises VALUES (69, 'urn:musclecuties:seed:v1:b63e9bee-ebbf-45ac-ac0c-74f6a97fd540', 'INCLINE_PUSH_UP', 'Incline Push-Up', 'A scalable upper-body push pattern for chest, shoulders, and triceps.', NULL, NULL, NULL, 'Chest', 'FrontShoulders,Triceps', 'Wrist,Shoulder', false);
INSERT INTO catalog.exercises VALUES (70, 'urn:musclecuties:seed:v1:8709151d-ab94-41ef-84b5-76b343ab89a2', 'INCLINE_DUMBBELL_PRESS', 'Incline Dumbbell Press', 'A chest and shoulder press that supports upper-body strength progression.', NULL, NULL, NULL, 'Chest', 'FrontShoulders,Triceps', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (71, 'urn:musclecuties:seed:v1:bef37475-90bc-4ae6-b9d9-843209d370a9', 'DUMBBELL_ROW', 'Dumbbell Row', 'A back-strength exercise for lats, upper back, and shoulder support.', NULL, NULL, NULL, 'UpperBack', 'Biceps,RearShoulders', '', true);
INSERT INTO catalog.exercises VALUES (72, 'urn:musclecuties:seed:v1:b33b0f7a-ab99-43ad-aede-4c2dcdaced74', 'SINGLE_ARM_CABLE_ROW', 'Single-Arm Cable Row', 'A unilateral cable row for lats, upper back, and balanced pulling strength.', NULL, NULL, NULL, 'UpperBack', 'Biceps,RearShoulders', '', true);
INSERT INTO catalog.exercises VALUES (73, 'urn:musclecuties:seed:v1:bf48236e-8167-41b0-9fd4-76c804b6bae9', 'ASSISTED_PULL_UP', 'Assisted Pull-Up', 'A vertical pull progression for back width, biceps, grip, and torso control.', NULL, NULL, NULL, 'UpperBack', 'Biceps,Forearms', '', true);
INSERT INTO catalog.exercises VALUES (74, 'urn:musclecuties:seed:v1:d3028b8d-23cc-493d-87f4-ce25c32c980c', 'OVERHEAD_PRESS', 'Overhead Press', 'A shoulder press pattern for upper-body strength.', NULL, NULL, NULL, 'FrontShoulders', 'SideShoulders,Triceps', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (75, 'urn:musclecuties:seed:v1:fb105b83-94da-47ff-8719-05a3b215c085', 'LATERAL_RAISE', 'Lateral Raise', 'A shoulder accessory movement for side delts.', NULL, NULL, NULL, 'SideShoulders', '', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (76, 'urn:musclecuties:seed:v1:42acadaa-b635-471f-bc8d-b73e9d624fc8', 'REAR_DELT_FLY', 'Rear Delt Fly', 'A rear-shoulder accessory for posture, shoulder balance, and upper-back detail.', NULL, NULL, NULL, 'RearShoulders', 'UpperBack', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (77, 'urn:musclecuties:seed:v1:5ebd5137-2ce8-4eb5-843b-6355a42166a5', 'BICEPS_CURL', 'Biceps Curl', 'A simple arm accessory movement.', NULL, NULL, NULL, 'Biceps', 'Forearms', 'Elbow', false);
INSERT INTO catalog.exercises VALUES (78, 'urn:musclecuties:seed:v1:fb20f897-e3d9-40b0-917b-32c64c73c7f4', 'TRICEPS_PRESSDOWN', 'Triceps Pressdown', 'A simple arm accessory movement for triceps.', NULL, NULL, NULL, 'Triceps', '', 'Elbow', false);
INSERT INTO catalog.exercises VALUES (79, 'urn:musclecuties:seed:v1:6a09acc3-1092-4097-a7a3-39b6fd620bb9', 'COPENHAGEN_SIDE_PLANK', 'Copenhagen Side Plank', 'An advanced side-plank variation for adductors, obliques, and hip stability.', NULL, NULL, NULL, 'Adductors', 'Obliques,Abs', 'Hip', false);
INSERT INTO catalog.exercises VALUES (80, 'urn:musclecuties:seed:v1:51881eda-f6a0-4fd3-9057-2c5584e0e53c', 'CABLE_WOODCHOP', 'Cable Woodchop', 'A rotational core drill for obliques, rib control, and athletic trunk strength.', NULL, NULL, NULL, 'Obliques', 'Abs', '', true);
INSERT INTO catalog.exercises VALUES (81, 'urn:musclecuties:seed:v1:8314f825-fb40-4e62-a61d-8bdba49d77ff', 'PLANK', 'Plank', 'A core endurance exercise for trunk stiffness and shoulder support.', NULL, NULL, NULL, 'Abs', 'FrontShoulders', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (82, 'urn:musclecuties:seed:v1:ff2236c6-889d-48d3-98a4-633f9e73bded', 'HANGING_KNEE_RAISE', 'Hanging Knee Raise', 'A hip-flexion and lower-ab drill for advanced trunk control.', NULL, NULL, NULL, 'Abs', 'HipFlexors,Forearms', 'Shoulder', false);
INSERT INTO catalog.exercises VALUES (83, 'urn:musclecuties:seed:v1:ea46ec9a-3e32-4f45-a1b6-76c1533c0721', 'REVERSE_CRUNCH', 'Reverse Crunch', 'A controlled lower-ab pattern that trains pelvis position without spinal loading.', NULL, NULL, NULL, 'Abs', 'HipFlexors', '', true);
INSERT INTO catalog.exercises VALUES (84, 'urn:musclecuties:seed:v1:183bd5c1-08be-495e-8c71-663c1bf7419d', 'BIKE_INTERVALS', 'Bike Intervals', 'Low-impact conditioning intervals for cardio capacity.', NULL, NULL, NULL, 'Quads', 'Glutes,Calves', '', true);
INSERT INTO catalog.exercises VALUES (85, 'urn:musclecuties:seed:v1:391b77a3-b164-4c17-b559-12d9e1fc440d', 'HIIT_INTERVALS', 'HIIT Intervals', 'Short hard conditioning blocks with enough recovery to keep each interval powerful.', NULL, NULL, NULL, 'Quads', 'Glutes,Calves,Abs', 'Knee', false);
INSERT INTO catalog.exercises VALUES (86, 'urn:musclecuties:seed:v1:92ce8476-20ba-4d1f-bfd9-86b5c58e74ac', 'CYCLING_INTERVALS', 'Cycling Intervals', 'Bike-based conditioning for stronger legs and repeatable aerobic power.', NULL, NULL, NULL, 'Quads', 'Glutes,Calves', '', true);
INSERT INTO catalog.exercises VALUES (87, 'urn:musclecuties:seed:v1:2655882e-73d8-45cb-ae46-937545d12d3c', 'RUNNING_INTERVALS', 'Running Intervals', 'Run-based conditioning built around pace, heart rate, and clean repeat efforts.', NULL, NULL, NULL, 'Calves', 'Glutes,Quads,Hamstrings', 'Knee', false);
INSERT INTO catalog.exercises VALUES (88, 'urn:musclecuties:seed:v1:5f4f7f0a-9c77-4b5b-bd76-74f528841019', 'TEMPO_RUN', 'Tempo Run', 'A controlled faster run for aerobic capacity without turning every minute into a sprint.', NULL, NULL, NULL, 'Calves', 'Glutes,Quads,Hamstrings', 'Knee', false);
INSERT INTO catalog.exercises VALUES (89, 'urn:musclecuties:seed:v1:f287e1a3-cf2b-4ead-80bf-f1070a86eee0', 'EASY_RUN', 'Easy Run', 'A low-stress jog for easy aerobic work and recovery-friendly movement.', NULL, NULL, NULL, 'Calves', 'Glutes,Quads,Hamstrings', 'Knee', false);
INSERT INTO catalog.exercises VALUES (90, 'urn:musclecuties:seed:v1:f0a1b168-4cef-42c5-a9bd-bb35a8f5fed5', 'ZONE_2_RIDE', 'Zone 2 Ride', 'Steady low-impact cardio for endurance and recovery.', NULL, NULL, NULL, 'Quads', 'Glutes,Calves', '', true);
INSERT INTO catalog.exercises VALUES (91, 'urn:musclecuties:seed:v1:cd60bb39-830e-4fb8-a4e0-1b49032e421c', 'EASY_WALK', 'Easy Walk', 'Gentle low-impact movement for recovery days.', NULL, NULL, NULL, 'Calves', 'Glutes,Quads', '', true);
INSERT INTO catalog.exercises VALUES (92, 'urn:musclecuties:seed:v1:ab6000f5-39d0-434f-908e-57dfe94669d9', 'MOBILITY_FLOW', 'Mobility Flow', 'A full-body mobility sequence for recovery and joint range.', NULL, NULL, NULL, 'HipFlexors', 'Abs,Glutes', '', true);
INSERT INTO catalog.exercises VALUES (93, 'urn:musclecuties:seed:v1:21a0dc6c-033c-48b6-b0e5-ebe753f48128', 'YOGA_FLOW', 'Yoga Flow', 'A recovery-focused flow for breath, mobility, and light strength.', NULL, NULL, NULL, 'Abs', 'Glutes,HipFlexors', '', true);
INSERT INTO catalog.exercises VALUES (94, 'urn:musclecuties:seed:v1:fa637473-f474-404f-8806-ac1c5b523dcd', 'SLOW_FLOW_YOGA', 'Slow Flow Yoga', 'A longer steady yoga practice for breath, positions, and calm strength.', NULL, NULL, NULL, 'Abs', 'Glutes,HipFlexors', '', true);
INSERT INTO catalog.exercises VALUES (95, 'urn:musclecuties:seed:v1:edab24d4-7cf0-4f4d-bc2b-c47193a096a5', 'HIP_OPENING_YOGA', 'Hip Opening Yoga', 'A lower-body yoga sequence for hips, glutes, adductors, and relaxed range.', NULL, NULL, NULL, 'HipFlexors', 'Glutes,Adductors', '', true);
INSERT INTO catalog.exercises VALUES (96, 'urn:musclecuties:seed:v1:b4c29cce-07c2-4afa-b17e-43f504666146', 'VINYASA_FLOW', 'Vinyasa Flow', 'A continuous yoga flow for heat, balance, breath, and controlled transitions.', NULL, NULL, NULL, 'Abs', 'Glutes,FrontShoulders', 'Shoulder,Wrist', false);
INSERT INTO catalog.exercises VALUES (97, 'urn:musclecuties:seed:v1:787b0ccd-4eee-4a7c-be87-b27c42ec14c5', 'POWER_YOGA', 'Power Yoga', 'A stronger yoga practice for heat, balance, and control.', NULL, NULL, NULL, 'Abs', 'Glutes,FrontShoulders', 'Shoulder,Wrist', false);
INSERT INTO catalog.exercises VALUES (98, 'urn:musclecuties:seed:v1:8539257d-809c-4a12-88b8-a18c5886468f', 'YIN_YOGA', 'Yin Yoga', 'Slow supported holds for hips, back, and nervous-system downshift.', NULL, NULL, NULL, 'HipFlexors', 'Glutes,LowerBack', '', true);
INSERT INTO catalog.exercises VALUES (99, 'urn:musclecuties:seed:v1:44c94759-c52d-424f-ad99-88ccaab1339e', 'RESTORATIVE_YOGA', 'Restorative Yoga', 'A gentle recovery practice built around breath and supported positions.', NULL, NULL, NULL, 'Abs', 'HipFlexors', '', true);
INSERT INTO catalog.exercises VALUES (100, 'urn:musclecuties:seed:v1:21c10cc8-dc93-484e-abf9-a4b100cf8e82', 'PILATES_FLOW', 'Pilates Flow', 'A core-control session with smooth transitions and steady breathing.', NULL, NULL, NULL, 'Abs', 'Obliques,HipFlexors', '', true);
INSERT INTO catalog.exercises VALUES (101, 'urn:musclecuties:seed:v1:6cd26a1e-4f2c-4245-9e7d-6f98ddc0477c', 'ACTIVE_RECOVERY_FLOW', 'Active Recovery Flow', 'Light movement for circulation, mobility, and better recovery.', NULL, NULL, NULL, 'HipFlexors', 'Abs,Glutes', '', true);
INSERT INTO catalog.exercises VALUES (102, 'urn:musclecuties:seed:v1:f34d6971-288d-45b4-84ec-54c4966c1642', 'BREATHING_RESET', 'Breathing Reset', 'A downshift drill for calm breathing and relaxed bracing.', NULL, NULL, NULL, 'Abs', '', '', true);
INSERT INTO catalog.exercises VALUES (103, 'urn:musclecuties:seed:v1:19811e72-bb0e-4ee5-a31a-c69b92b8153b', 'SWIMMING', 'Swimming', 'Low-impact cardio for smooth conditioning and joint-friendly volume.', NULL, NULL, NULL, 'UpperBack', 'Chest,Glutes,Quads', '', true);
INSERT INTO catalog.exercises VALUES (104, 'urn:musclecuties:seed:v1:1975c915-e95d-4fda-9970-24b767a47fbf', 'DANCE_CARDIO', 'Dance Cardio', 'Rhythmic conditioning that builds coordination and aerobic work capacity.', NULL, NULL, NULL, 'Calves', 'Glutes,Quads,Abs', '', true);


--
-- Data for Name: food_items; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.food_items VALUES (1, 'urn:musclecuties:seed:v1:13beb208-be92-4686-9ace-babebc89f014', 'Carrot, raw', 145, 13, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (2, 'urn:musclecuties:seed:v1:4230f26c-907e-4dd6-853e-6c59323b6045', 'Olive oil', 170, 16, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 172187, 'Foundation', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (3, 'urn:musclecuties:seed:v1:93f9c92a-bc4d-47fc-9c1b-feb5fd2b5622', 'Oats, rolled, dry', 195, 19, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 173904, 'Foundation', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (4, 'urn:musclecuties:seed:v1:c07b76c4-50e5-49e2-9587-951b40ab6b71', 'Certified gluten-free rolled oats, dry', 220, 10, 28, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (5, 'urn:musclecuties:seed:v1:a7e826d7-997f-4f3c-b083-191294bc1a99', 'Eggs, whole, raw', 120, 13, 32, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 174161, 'Foundation', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (6, 'urn:musclecuties:seed:v1:1ed4c935-041a-4c46-ba51-a04f6bcfe0c3', 'Chicken breast, raw', 145, 16, 12, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 331960, 'Foundation', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (7, 'urn:musclecuties:seed:v1:9cf0d559-2064-4110-84ea-c43614dfc532', 'Spinach, raw', 170, 19, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 173428, 'Foundation', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (8, 'urn:musclecuties:seed:v1:a8af2ea8-5414-4314-81b2-e2a780ab6bf2', 'Cannellini beans, cooked', 195, 10, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (9, 'urn:musclecuties:seed:v1:65301017-3f0e-4801-93b1-b8bc7c93b1fa', 'Black beans, cooked', 220, 13, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (10, 'urn:musclecuties:seed:v1:7bbb7bbb-b5ac-422e-a558-50cf6563e592', 'Chickpeas, cooked', 120, 16, 28, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (11, 'urn:musclecuties:seed:v1:6cf21d8e-f3dc-4ece-84bc-8359cdb48d0b', 'Lentils, cooked', 145, 19, 32, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (12, 'urn:musclecuties:seed:v1:175a2926-dc28-42ee-9e25-f66321260e5a', 'Quinoa, cooked', 170, 10, 12, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (13, 'urn:musclecuties:seed:v1:387f6576-d08e-49ee-acb1-692cd0c53ddc', 'Marinara sauce', 195, 13, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (14, 'urn:musclecuties:seed:v1:b40bc559-96ab-4ff0-90e9-c4ef81dabbce', 'Tomato paste', 220, 16, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (15, 'urn:musclecuties:seed:v1:a0ebd7cd-ad4a-4b7f-88b6-634dca9ceac4', 'Mozzarella cheese, part-skim', 120, 19, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (16, 'urn:musclecuties:seed:v1:91fac2ba-c67b-44c4-bfc3-dd881bd9e773', 'Parmesan cheese', 145, 10, 28, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (17, 'urn:musclecuties:seed:v1:704007a4-7fb5-4f8b-ad2a-fe6b45386d2a', 'Turkey pepperoni', 170, 13, 32, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (18, 'urn:musclecuties:seed:v1:d6b5529b-7ec1-4b9b-8cca-994bbe3f0200', 'Firm tofu, calcium-set', 195, 16, 12, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (19, 'urn:musclecuties:seed:v1:4e2aba62-9e7d-4830-b2b9-7d0f5b8f60fd', 'Nutritional yeast, fortified', 220, 19, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (20, 'urn:musclecuties:seed:v1:960428bc-4469-445e-8858-403ee050e7ed', 'Red bell pepper, raw', 120, 10, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (21, 'urn:musclecuties:seed:v1:43d1746e-a1cb-495f-9355-480b7abb8f1c', 'Kale, raw', 145, 13, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (22, 'urn:musclecuties:seed:v1:6e855b39-a762-474e-88eb-7d16369884b4', 'Sweet potato, baked', 170, 16, 28, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (23, 'urn:musclecuties:seed:v1:320875bd-bf94-468d-b667-7f022ae56b02', 'Avocado, raw', 195, 19, 32, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (24, 'urn:musclecuties:seed:v1:7cb28fd6-5aa1-4e50-a21e-bed96bc6882a', 'Greek yogurt, plain nonfat', 220, 10, 12, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (25, 'urn:musclecuties:seed:v1:654d9db4-07f9-4004-9435-9fd3ee2be3f6', 'Cottage cheese, low-fat', 120, 13, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (26, 'urn:musclecuties:seed:v1:ab51e88a-f29d-4ad1-bfff-27b6314b9e73', 'Blueberries, raw', 145, 16, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (27, 'urn:musclecuties:seed:v1:267fc209-f4a8-457a-967a-adea6eb4318f', 'Pumpkin seeds', 170, 19, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (28, 'urn:musclecuties:seed:v1:f7d86f48-cd1f-4eb2-b806-6062fad8938a', 'Salmon, cooked', 195, 10, 28, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (29, 'urn:musclecuties:seed:v1:82b6d47f-5571-4b1c-a5fc-8ef5a96a874f', 'Tuna, canned in water', 220, 13, 32, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (30, 'urn:musclecuties:seed:v1:d98e925f-edf4-468b-92b9-278984145064', 'Hummus', 120, 16, 12, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (31, 'urn:musclecuties:seed:v1:0d142e9f-69ee-4a20-9315-594dacf90ed8', 'Chia seeds', 145, 19, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (32, 'urn:musclecuties:seed:v1:eb57c10c-1db3-406b-86fe-bf554e7b653b', 'Fortified soy milk, unsweetened', 170, 10, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (33, 'urn:musclecuties:seed:v1:aa27bdf9-47ca-4dea-b75d-441e2a9047a3', 'White potato, baked', 195, 13, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (34, 'urn:musclecuties:seed:v1:86f87bf4-eab3-4331-903c-fc70e77a48f4', 'Whole wheat bread', 220, 16, 28, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (35, 'urn:musclecuties:seed:v1:55b0cc42-6876-4f00-8ff0-d7bd53b269ee', 'Flour tortilla, whole wheat', 120, 19, 32, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (36, 'urn:musclecuties:seed:v1:151986ec-e325-498b-8e6d-9806e6e724f2', 'Hamburger bun, whole wheat', 145, 10, 12, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (37, 'urn:musclecuties:seed:v1:b19c54d8-3a06-4e66-97f1-d95172aea6a2', 'Pinto beans, cooked', 170, 13, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (38, 'urn:musclecuties:seed:v1:d348ddc0-a066-4e57-9508-dfe33e5d20f0', 'Brown rice, cooked', 195, 16, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (39, 'urn:musclecuties:seed:v1:3edc5a43-5cfd-4e5a-a5cb-9f8cf1810a8c', 'Turkey ham, sliced', 220, 19, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (40, 'urn:musclecuties:seed:v1:95dd65e6-9ba7-42f2-8bd0-4daabc447001', 'Turkey sausage', 120, 10, 28, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (41, 'urn:musclecuties:seed:v1:25893a74-41e8-4011-b535-b43c52ad75a3', 'Ground turkey, raw', 145, 13, 32, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (42, 'urn:musclecuties:seed:v1:b2cd1ac6-dc7c-4428-8842-eab46580b3a5', 'Ground beef, 90% lean', 170, 16, 12, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (43, 'urn:musclecuties:seed:v1:428a4296-b6b4-4345-a0ba-bc5d36d8835a', 'Ground chicken, raw', 195, 19, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (44, 'urn:musclecuties:seed:v1:60941ad8-f714-4879-a865-32e0e409549a', 'Lettuce, romaine', 220, 10, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (45, 'urn:musclecuties:seed:v1:70001df4-c692-4ae1-8a4c-4d28ef9312c9', 'Onion, raw', 120, 13, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (46, 'urn:musclecuties:seed:v1:46c23102-ab19-47ea-8a94-0d9e4f3023eb', 'Tomato, raw', 145, 16, 28, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (47, 'urn:musclecuties:seed:v1:1e5697d3-9401-4e33-bb32-421cb7a54ee8', 'Strawberries, raw', 170, 19, 32, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (48, 'urn:musclecuties:seed:v1:b9b0f408-04d2-4dee-bac8-a25672f1c945', 'Raspberries, raw', 195, 10, 12, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (49, 'urn:musclecuties:seed:v1:a3a6d650-fbc8-4d93-b08a-730fecadb1d1', 'Banana, raw', 220, 13, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (50, 'urn:musclecuties:seed:v1:a1cb51b9-30ff-4953-9e19-4caa72abdca1', 'Crushed tomatoes, canned', 120, 16, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (51, 'urn:musclecuties:seed:v1:4dcbf919-f8ce-48ba-9582-e754a4d275c4', 'Sriracha sauce', 145, 19, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (52, 'urn:musclecuties:seed:v1:30c4ad65-abc5-4038-81aa-37c9c8ce6705', 'Sour cream, reduced fat', 170, 10, 28, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (53, 'urn:musclecuties:seed:v1:58ba3d4c-32e2-4fa9-90e2-407da188872a', 'Yellow mustard', 195, 13, 32, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (54, 'urn:musclecuties:seed:v1:367d1f83-dfdb-4512-ba44-8dd059186c67', 'Ketchup, zero sugar', 220, 16, 12, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (55, 'urn:musclecuties:seed:v1:e9a30a11-11c7-4434-bce2-efc008c92ed7', 'Hot sauce, generic', 120, 19, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (56, 'urn:musclecuties:seed:v1:7b455f18-8758-4045-b842-f80210e9b0c2', 'Greek yogurt, plain 2%', 145, 10, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (57, 'urn:musclecuties:seed:v1:2d8dfda3-7eb4-4191-87df-f9e79c95121c', 'Apple cider vinegar', 170, 13, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (58, 'urn:musclecuties:seed:v1:5ee2a840-3a64-4e54-98af-ecdb6d79f4fc', 'White vinegar, distilled', 195, 16, 28, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (59, 'urn:musclecuties:seed:v1:7721bdf8-f962-46bc-a7f8-86b3e44fd9d1', 'Salsa, no sugar added', 220, 19, 32, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (60, 'urn:musclecuties:seed:v1:43313937-eed2-4c02-9d9f-91be60ba92f1', 'Honey', 120, 10, 12, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (61, 'urn:musclecuties:seed:v1:7d2d7d38-7dee-416e-b07d-4a60796ff3db', 'Peanut butter, natural', 145, 13, 16, 6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (62, 'urn:musclecuties:seed:v1:cbcbc046-08e7-4846-ba67-cd5975537ab6', 'Dark chocolate, 70%', 170, 16, 20, 8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');
INSERT INTO catalog.food_items VALUES (63, 'urn:musclecuties:seed:v1:58ca469a-d880-46b9-ba69-9e8c5e1b33db', 'Maple syrup', 195, 19, 24, 4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'CanonicalStarter', NULL, NULL, NULL, NULL, 100, 'g', NULL, NULL, '2026-10-17 00:00:00+00', '2026-10-17 00:00:00+00');


--
-- Data for Name: food_item_versions; Type: TABLE DATA; Schema: catalog; Owner: -
--



--
-- Data for Name: goal_tier_weights; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.goal_tier_weights VALUES (1, 'urn:musclecuties:seed:v1:10a3ff40-5da4-41b5-ad19-48ba0dce6a6f', 'MuscleTone', 1, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (2, 'urn:musclecuties:seed:v1:2a41e92d-dc5f-4560-b1c6-1f60657171a1', 'MuscleTone', 3, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (3, 'urn:musclecuties:seed:v1:0bb6f1b3-7208-4213-85a7-a76b42d3d95d', 'MuscleTone', 2, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (4, 'urn:musclecuties:seed:v1:2a428946-a70a-44ba-aa67-5381663880e5', 'MuscleTone', 6, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (5, 'urn:musclecuties:seed:v1:dc44fac2-8ad0-46d7-8d81-963e4778f3c1', 'MuscleTone', 7, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (6, 'urn:musclecuties:seed:v1:d612aa96-da07-4ecd-93e0-8b6386974422', 'MuscleTone', 12, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (7, 'urn:musclecuties:seed:v1:5c612fb7-8c08-4dea-b488-6a239d9485f7', 'MuscleTone', 8, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (8, 'urn:musclecuties:seed:v1:b8b1ba9d-74ad-4ed7-a689-39a6cf9377df', 'MuscleTone', 10, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (9, 'urn:musclecuties:seed:v1:7a269fa2-2b20-486f-aacc-1d096100550b', 'MuscleTone', 11, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (10, 'urn:musclecuties:seed:v1:c2941216-7704-4b72-8c9e-7eee8a64f733', 'MuscleTone', 5, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (11, 'urn:musclecuties:seed:v1:a414be11-ee7c-4850-a1c7-96e6b1a38651', 'MuscleTone', 9, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (12, 'urn:musclecuties:seed:v1:ec9e2324-6efd-46f9-9c36-eb04f541616e', 'MuscleTone', 4, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (13, 'urn:musclecuties:seed:v1:4195f6bd-6e0b-4f6e-9a84-ea6b85ffc827', 'MuscleTone', 13, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (14, 'urn:musclecuties:seed:v1:e8f662e8-550b-4fda-a310-9ba3a2e32df6', 'Strength', 1, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (15, 'urn:musclecuties:seed:v1:33c54364-b5a1-4f39-98e0-fbd1eb4c3f41', 'Strength', 3, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (16, 'urn:musclecuties:seed:v1:e1d5f315-01f0-4356-b13d-8af9587dcae9', 'Strength', 2, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (17, 'urn:musclecuties:seed:v1:baac2a09-b404-4987-8d93-b1ac7bc0aa26', 'Strength', 6, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (18, 'urn:musclecuties:seed:v1:c093397e-98f4-44e8-9c03-c3ee79b0ce4d', 'Strength', 7, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (19, 'urn:musclecuties:seed:v1:b0567ffa-9d7c-44d3-9e4e-f5451811e10a', 'Strength', 12, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (20, 'urn:musclecuties:seed:v1:a0989c91-9a22-4e1d-87ed-967be7d44fc9', 'Strength', 8, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (21, 'urn:musclecuties:seed:v1:3080ded1-28ee-4e29-aeaa-b325c8d5abd4', 'Strength', 10, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (22, 'urn:musclecuties:seed:v1:a3f6acd8-e8e4-468e-8165-3e6b3188d59a', 'Strength', 11, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (23, 'urn:musclecuties:seed:v1:37f08a86-f7a8-43d5-bf9e-070d96927c26', 'Strength', 5, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (24, 'urn:musclecuties:seed:v1:d6c02b1b-5db8-4263-8a29-ea7e1424ebdb', 'Strength', 9, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (25, 'urn:musclecuties:seed:v1:f5759818-78f3-4bc3-948d-b5341a69e44f', 'Strength', 4, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (26, 'urn:musclecuties:seed:v1:6e4715fe-f32a-49ae-a6b0-2866ed632f83', 'Strength', 13, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (27, 'urn:musclecuties:seed:v1:1d551ce4-babe-48ee-ae8a-b55b79b71288', 'FatLoss', 1, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (28, 'urn:musclecuties:seed:v1:6b90e3a4-bb75-4a61-b51b-aabe6e0d0fb6', 'FatLoss', 3, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (29, 'urn:musclecuties:seed:v1:30e65a6a-4b00-487e-a1a7-a18f25be32aa', 'FatLoss', 2, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (30, 'urn:musclecuties:seed:v1:cdc7e0c5-d9c2-470a-a9ac-a2229fbe2eb4', 'FatLoss', 6, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (31, 'urn:musclecuties:seed:v1:bd022fca-7401-4cbf-b76e-0ef9633fa9c0', 'FatLoss', 7, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (32, 'urn:musclecuties:seed:v1:f7b7633c-a3fb-46da-943b-38b97ead1ed5', 'FatLoss', 12, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (33, 'urn:musclecuties:seed:v1:4c24d77e-7326-4a4b-bc37-1b0f3a59f849', 'FatLoss', 8, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (34, 'urn:musclecuties:seed:v1:3a2b9dbd-53d6-4951-b088-a1f5c540a73f', 'FatLoss', 10, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (35, 'urn:musclecuties:seed:v1:86da1afa-206b-4d33-9075-20df5a0ff2a1', 'FatLoss', 11, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (36, 'urn:musclecuties:seed:v1:f2995ea2-90c3-453c-8d20-0b7c6ae65064', 'FatLoss', 5, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (37, 'urn:musclecuties:seed:v1:f192664a-2723-4768-9413-61e1871f6346', 'FatLoss', 9, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (38, 'urn:musclecuties:seed:v1:85422f1f-2c13-4c2e-aa99-289b112e9b0d', 'FatLoss', 4, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (39, 'urn:musclecuties:seed:v1:ab2f705a-2c3d-4af3-8a92-fd05e6c9af9e', 'FatLoss', 13, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (40, 'urn:musclecuties:seed:v1:5995c94f-3bd3-4848-91e5-27c394ea4aeb', 'MaintainHealth', 1, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (41, 'urn:musclecuties:seed:v1:0c74871a-4e3c-4689-9dc5-7e7e01b9abaa', 'MaintainHealth', 3, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (42, 'urn:musclecuties:seed:v1:bc3c3f23-f78a-47fa-a664-53f07fac3bbf', 'MaintainHealth', 2, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (43, 'urn:musclecuties:seed:v1:492ab611-ed19-4dc6-9343-c1298e002fae', 'MaintainHealth', 6, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (44, 'urn:musclecuties:seed:v1:1d0ec0e4-39d2-415c-86a6-2fbf181e9996', 'MaintainHealth', 7, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (45, 'urn:musclecuties:seed:v1:434461f9-797b-4cee-a344-8830bfd5c5d0', 'MaintainHealth', 12, 'A');
INSERT INTO catalog.goal_tier_weights VALUES (46, 'urn:musclecuties:seed:v1:9b83f55f-4141-45cc-8b01-ae56612bb554', 'MaintainHealth', 8, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (47, 'urn:musclecuties:seed:v1:eea04dcb-f540-4449-86d8-858d7d7630cb', 'MaintainHealth', 10, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (48, 'urn:musclecuties:seed:v1:6c98bb87-5e4a-4a65-9c17-439f312a3153', 'MaintainHealth', 11, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (49, 'urn:musclecuties:seed:v1:021f10a8-ce32-413c-bcd4-d036cf5c0915', 'MaintainHealth', 5, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (50, 'urn:musclecuties:seed:v1:6dc00f57-54e6-465e-a1df-a006337e0719', 'MaintainHealth', 9, 'B');
INSERT INTO catalog.goal_tier_weights VALUES (51, 'urn:musclecuties:seed:v1:d20dbfc2-7f55-43ce-ab20-ed4c69d9b7e6', 'MaintainHealth', 4, 'C');
INSERT INTO catalog.goal_tier_weights VALUES (52, 'urn:musclecuties:seed:v1:5f8e77f5-2552-4b10-bed5-5fd21db0e960', 'MaintainHealth', 13, 'C');


--
-- Data for Name: meal_templates; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.meal_templates VALUES (1, 'urn:musclecuties:seed:v1:33e460dc-2d35-42d6-a246-2947d9d5bc0d', 'Margherita Pizza Beans', 'Canonical system meal template: Margherita Pizza Beans', 'Breakfast', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 1, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (2, 'urn:musclecuties:seed:v1:3dec9734-4130-4bbb-8c0f-71c6000b5001', 'Vegan Pizza Beans', 'Canonical system meal template: Vegan Pizza Beans', 'Lunch', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 2, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (3, 'urn:musclecuties:seed:v1:dec3949d-3d04-4e7b-aff9-1199c4c6f6e9', 'Pepperoni Pizza Beans', 'Canonical system meal template: Pepperoni Pizza Beans', 'Dinner', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 3, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (4, 'urn:musclecuties:seed:v1:9024f2b4-e74a-4fbf-91ef-e12cc938589c', 'Gluten-Free Pizza Beans', 'Canonical system meal template: Gluten-Free Pizza Beans', 'Snack', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 4, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (5, 'urn:musclecuties:seed:v1:27506a89-3493-4016-9d8f-4ddef4862613', 'Chicken Quinoa Power Bowl', 'Canonical system meal template: Chicken Quinoa Power Bowl', 'Breakfast', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 5, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (6, 'urn:musclecuties:seed:v1:a81c337a-f3f8-45a4-a257-1ba8da096dfb', 'Salmon Sweet Potato Plate', 'Canonical system meal template: Salmon Sweet Potato Plate', 'Lunch', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 6, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (7, 'urn:musclecuties:seed:v1:3149d6b9-0b06-4692-9d25-6c1d86c862d3', 'Tofu Lentil Iron Bowl', 'Canonical system meal template: Tofu Lentil Iron Bowl', 'Dinner', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 7, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (8, 'urn:musclecuties:seed:v1:b50e3048-8a7e-4bba-8f29-3f88b45c1392', 'Turkey Burger Bowl', 'Canonical system meal template: Turkey Burger Bowl', 'Snack', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 8, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (9, 'urn:musclecuties:seed:v1:668e8419-0e40-4916-babf-2951394119a4', 'Greek Yogurt Berry Crunch', 'Canonical system meal template: Greek Yogurt Berry Crunch', 'Breakfast', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 9, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (10, 'urn:musclecuties:seed:v1:6c8397bf-934d-49ea-b914-2c01d4925058', 'Oats Peanut Butter Berry Bowl', 'Canonical system meal template: Oats Peanut Butter Berry Bowl', 'Lunch', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 10, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (11, 'urn:musclecuties:seed:v1:1d74f584-b4e7-434f-9d73-b1848dc925d6', 'Gluten-Free Oat Protein Bowl', 'Canonical system meal template: Gluten-Free Oat Protein Bowl', 'Dinner', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 11, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (12, 'urn:musclecuties:seed:v1:3903d37d-9e2f-463b-b761-f2c23fb64b40', 'Egg Avocado Toast', 'Canonical system meal template: Egg Avocado Toast', 'Snack', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 12, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (13, 'urn:musclecuties:seed:v1:40007119-fca5-4146-9de4-4e48e2baa1eb', 'Tuna Hummus Wrap', 'Canonical system meal template: Tuna Hummus Wrap', 'Breakfast', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 13, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (14, 'urn:musclecuties:seed:v1:e9b2f634-c64e-45b5-ba7a-f8268fbec9a9', 'Black Bean Quinoa Taco Bowl', 'Canonical system meal template: Black Bean Quinoa Taco Bowl', 'Lunch', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 14, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (15, 'urn:musclecuties:seed:v1:47104642-8795-432e-b0b5-ee89f56f672e', 'Cottage Cheese Berry Bowl', 'Canonical system meal template: Cottage Cheese Berry Bowl', 'Dinner', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 15, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (16, 'urn:musclecuties:seed:v1:9515fad6-124e-4b7a-b07f-c167dc025aed', 'Hummus Sweet Potato Plate', 'Canonical system meal template: Hummus Sweet Potato Plate', 'Snack', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 16, true, '2026-10-17 00:00:00+00');
INSERT INTO catalog.meal_templates VALUES (17, 'urn:musclecuties:seed:v1:a1c2a7c8-cf8b-47c0-b6aa-d8c28f0fdb1a', 'Chicken Potato Recovery Plate', 'Canonical system meal template: Chicken Potato Recovery Plate', 'Breakfast', 'None', 'Menstrual,Follicular,Ovulatory,Luteal', 17, true, '2026-10-17 00:00:00+00');


--
-- Data for Name: meal_template_entries; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.meal_template_entries VALUES (1, 'urn:musclecuties:seed:v1:e4060bf3-affb-49b2-8c26-6570824a194a', 1, 8, 180.0);
INSERT INTO catalog.meal_template_entries VALUES (2, 'urn:musclecuties:seed:v1:196b99b2-02fd-4d16-808b-cb114b93a3b2', 1, 13, 90.0);
INSERT INTO catalog.meal_template_entries VALUES (3, 'urn:musclecuties:seed:v1:a4fab589-8a1e-4688-b6d9-307c5718e2f0', 1, 15, 35.0);
INSERT INTO catalog.meal_template_entries VALUES (4, 'urn:musclecuties:seed:v1:d79f7224-0cb8-4683-9856-f99b1cdb7b0c', 1, 16, 8.0);
INSERT INTO catalog.meal_template_entries VALUES (5, 'urn:musclecuties:seed:v1:4e0715c9-61ee-46e1-8856-8d1b54032d20', 1, 2, 8.0);
INSERT INTO catalog.meal_template_entries VALUES (6, 'urn:musclecuties:seed:v1:8a57cd80-a8e9-4005-b40b-006150dd05a3', 2, 8, 180.0);
INSERT INTO catalog.meal_template_entries VALUES (7, 'urn:musclecuties:seed:v1:cd03bc66-3a95-495a-842e-ade6144e1867', 2, 14, 35.0);
INSERT INTO catalog.meal_template_entries VALUES (8, 'urn:musclecuties:seed:v1:2e384729-d423-43d7-9453-a93ee81c8586', 2, 20, 80.0);
INSERT INTO catalog.meal_template_entries VALUES (9, 'urn:musclecuties:seed:v1:e533edae-de84-41ce-a289-80527cc93969', 2, 19, 16.0);
INSERT INTO catalog.meal_template_entries VALUES (10, 'urn:musclecuties:seed:v1:d28b27da-472d-445d-aaf9-015c9dd469f9', 2, 2, 8.0);
INSERT INTO catalog.meal_template_entries VALUES (11, 'urn:musclecuties:seed:v1:f079ce1b-0ce6-42ea-9c25-6d3cb97135bc', 3, 8, 170.0);
INSERT INTO catalog.meal_template_entries VALUES (12, 'urn:musclecuties:seed:v1:b19b77e2-a1d1-480d-a715-1562c261afc6', 3, 13, 90.0);
INSERT INTO catalog.meal_template_entries VALUES (13, 'urn:musclecuties:seed:v1:bc2d456d-6e01-4d1e-a9e5-bcfed790f409', 3, 17, 25.0);
INSERT INTO catalog.meal_template_entries VALUES (14, 'urn:musclecuties:seed:v1:253feb77-0ce4-4fef-8cba-9c92fd210768', 3, 15, 35.0);
INSERT INTO catalog.meal_template_entries VALUES (15, 'urn:musclecuties:seed:v1:800eefdb-3913-4504-bfe3-c277a9702a53', 3, 2, 6.0);
INSERT INTO catalog.meal_template_entries VALUES (16, 'urn:musclecuties:seed:v1:d5900f8b-44f5-4405-bd59-83ae8f511514', 4, 8, 180.0);
INSERT INTO catalog.meal_template_entries VALUES (17, 'urn:musclecuties:seed:v1:11e904d0-6405-466e-b9b5-d0b5748f6391', 4, 13, 90.0);
INSERT INTO catalog.meal_template_entries VALUES (18, 'urn:musclecuties:seed:v1:2fa4e352-7696-4461-9ad0-228194aefbec', 4, 15, 35.0);
INSERT INTO catalog.meal_template_entries VALUES (19, 'urn:musclecuties:seed:v1:d5188897-65d1-4462-938d-831e3f1d0ac1', 4, 21, 60.0);
INSERT INTO catalog.meal_template_entries VALUES (20, 'urn:musclecuties:seed:v1:410d0c29-e0ac-4815-b2c8-9e69b8de0b8f', 4, 2, 8.0);
INSERT INTO catalog.meal_template_entries VALUES (21, 'urn:musclecuties:seed:v1:73732883-b953-45de-9d8e-63f44cc563bd', 5, 6, 140.0);
INSERT INTO catalog.meal_template_entries VALUES (22, 'urn:musclecuties:seed:v1:fb22acab-c05c-4dda-90a4-6f5b9be49053', 5, 12, 150.0);
INSERT INTO catalog.meal_template_entries VALUES (23, 'urn:musclecuties:seed:v1:28f7759a-3d38-462f-8423-9dd98e489e60', 5, 7, 70.0);
INSERT INTO catalog.meal_template_entries VALUES (24, 'urn:musclecuties:seed:v1:9aaa3617-3f5c-4252-b303-49c776fc9fea', 5, 20, 80.0);
INSERT INTO catalog.meal_template_entries VALUES (25, 'urn:musclecuties:seed:v1:acceaab2-7954-4ccd-923d-102751b034d8', 5, 23, 50.0);
INSERT INTO catalog.meal_template_entries VALUES (26, 'urn:musclecuties:seed:v1:a249e3cf-364c-4dc4-b382-47e4933808e0', 6, 28, 130.0);
INSERT INTO catalog.meal_template_entries VALUES (27, 'urn:musclecuties:seed:v1:08d4badc-8573-42eb-b1d7-62262b452879', 6, 22, 180.0);
INSERT INTO catalog.meal_template_entries VALUES (28, 'urn:musclecuties:seed:v1:5d939d01-3708-4a27-9169-b7ee5605d523', 6, 21, 70.0);
INSERT INTO catalog.meal_template_entries VALUES (29, 'urn:musclecuties:seed:v1:b6160967-024c-48ce-add1-0dd4014268ff', 6, 2, 8.0);
INSERT INTO catalog.meal_template_entries VALUES (30, 'urn:musclecuties:seed:v1:42a12030-cf73-49d2-94b3-8f074b93c7ba', 7, 18, 140.0);
INSERT INTO catalog.meal_template_entries VALUES (31, 'urn:musclecuties:seed:v1:6284820c-ff82-4870-aec1-dbd5be44e48b', 7, 11, 150.0);
INSERT INTO catalog.meal_template_entries VALUES (32, 'urn:musclecuties:seed:v1:87ac3476-314b-44d7-840c-352880d1dd1a', 7, 7, 80.0);
INSERT INTO catalog.meal_template_entries VALUES (33, 'urn:musclecuties:seed:v1:17c4524f-6d50-4224-91e9-27226e289eb4', 7, 46, 100.0);
INSERT INTO catalog.meal_template_entries VALUES (34, 'urn:musclecuties:seed:v1:c2ecc829-a3e9-4b26-8a95-21413deceb5b', 8, 41, 140.0);
INSERT INTO catalog.meal_template_entries VALUES (35, 'urn:musclecuties:seed:v1:bd245ef9-553f-481d-a197-a8d16cfe9318', 8, 33, 180.0);
INSERT INTO catalog.meal_template_entries VALUES (36, 'urn:musclecuties:seed:v1:0b537e1e-d593-49fb-8c3c-399d97677fd5', 8, 44, 80.0);
INSERT INTO catalog.meal_template_entries VALUES (37, 'urn:musclecuties:seed:v1:d7954829-36e0-4ac9-b275-e5d2ae6cbb2c', 8, 46, 100.0);
INSERT INTO catalog.meal_template_entries VALUES (38, 'urn:musclecuties:seed:v1:0ade8bfb-a41c-4eb6-9e08-2db7fea95dca', 8, 53, 12.0);
INSERT INTO catalog.meal_template_entries VALUES (39, 'urn:musclecuties:seed:v1:106268ce-a453-4db5-a8c6-6d41f0771bbc', 9, 24, 220.0);
INSERT INTO catalog.meal_template_entries VALUES (40, 'urn:musclecuties:seed:v1:bb3adeff-4e10-430e-ba20-d20b66eb0838', 9, 26, 90.0);
INSERT INTO catalog.meal_template_entries VALUES (41, 'urn:musclecuties:seed:v1:75e88d6e-681c-49f1-bc35-e4520a0c02b3', 9, 31, 18.0);
INSERT INTO catalog.meal_template_entries VALUES (42, 'urn:musclecuties:seed:v1:cb9f9b9d-7e8e-454c-83d4-a8867f69bfd4', 9, 60, 10.0);
INSERT INTO catalog.meal_template_entries VALUES (43, 'urn:musclecuties:seed:v1:a21ea777-868e-4a91-8610-ef33c6d3b66b', 10, 3, 55.0);
INSERT INTO catalog.meal_template_entries VALUES (44, 'urn:musclecuties:seed:v1:9dcce648-dd2d-4339-80c8-9718d0b22069', 10, 32, 180.0);
INSERT INTO catalog.meal_template_entries VALUES (45, 'urn:musclecuties:seed:v1:8a240c79-295b-4a78-a35b-4ae317451533', 10, 61, 18.0);
INSERT INTO catalog.meal_template_entries VALUES (46, 'urn:musclecuties:seed:v1:f1f356f4-3337-4bb7-8b77-e617bc0a41c1', 10, 47, 100.0);
INSERT INTO catalog.meal_template_entries VALUES (47, 'urn:musclecuties:seed:v1:f80480bb-dc96-4654-bb45-3c8de4126188', 11, 4, 55.0);
INSERT INTO catalog.meal_template_entries VALUES (48, 'urn:musclecuties:seed:v1:ebfe88e0-ce01-4bbb-9da5-ae2f1f69795b', 11, 32, 180.0);
INSERT INTO catalog.meal_template_entries VALUES (49, 'urn:musclecuties:seed:v1:6b0694f7-f6fb-48b5-a66e-db95e0f0d546', 11, 26, 90.0);
INSERT INTO catalog.meal_template_entries VALUES (50, 'urn:musclecuties:seed:v1:0e2ed603-f3ec-46e9-af66-9455870b7f19', 11, 31, 16.0);
INSERT INTO catalog.meal_template_entries VALUES (51, 'urn:musclecuties:seed:v1:b7e84d74-30f4-4f53-a9cf-d89b127f4a71', 12, 34, 70.0);
INSERT INTO catalog.meal_template_entries VALUES (52, 'urn:musclecuties:seed:v1:bfbb3080-c42c-4502-bdfa-2d283b4dc67f', 12, 5, 100.0);
INSERT INTO catalog.meal_template_entries VALUES (53, 'urn:musclecuties:seed:v1:d22d8493-97a7-4240-9414-8491abf40573', 12, 23, 60.0);
INSERT INTO catalog.meal_template_entries VALUES (54, 'urn:musclecuties:seed:v1:1574e0a3-9362-438a-9f87-8f09b5d08c5c', 12, 46, 80.0);
INSERT INTO catalog.meal_template_entries VALUES (55, 'urn:musclecuties:seed:v1:7410a932-2fec-4ed8-9a56-62be7a125944', 13, 29, 120.0);
INSERT INTO catalog.meal_template_entries VALUES (56, 'urn:musclecuties:seed:v1:b0842275-e737-4c41-a053-9c3116aaf15d', 13, 35, 70.0);
INSERT INTO catalog.meal_template_entries VALUES (57, 'urn:musclecuties:seed:v1:ffb413d4-7820-4dcb-aa6e-e13e597b2356', 13, 30, 45.0);
INSERT INTO catalog.meal_template_entries VALUES (58, 'urn:musclecuties:seed:v1:2a335ead-ddf0-428e-86a0-0e3bb6d3db0c', 13, 44, 60.0);
INSERT INTO catalog.meal_template_entries VALUES (59, 'urn:musclecuties:seed:v1:e3b4e9d9-9e1a-4664-b78a-9eb3deac5e9a', 13, 20, 70.0);
INSERT INTO catalog.meal_template_entries VALUES (60, 'urn:musclecuties:seed:v1:4c73bf22-1af7-44a7-926a-d0e7aa49172a', 14, 9, 150.0);
INSERT INTO catalog.meal_template_entries VALUES (61, 'urn:musclecuties:seed:v1:3d3028da-ef8d-4859-a8d7-c9e965a0f6bd', 14, 12, 140.0);
INSERT INTO catalog.meal_template_entries VALUES (62, 'urn:musclecuties:seed:v1:5699258f-25a9-4960-b2fe-cd668b9b6999', 14, 23, 55.0);
INSERT INTO catalog.meal_template_entries VALUES (63, 'urn:musclecuties:seed:v1:07129cbb-46f7-4b0e-a7f3-716f56d761d8', 14, 46, 100.0);
INSERT INTO catalog.meal_template_entries VALUES (64, 'urn:musclecuties:seed:v1:b19f1df0-b78a-4bee-9144-1b401392e880', 14, 55, 10.0);
INSERT INTO catalog.meal_template_entries VALUES (65, 'urn:musclecuties:seed:v1:2eaaadc8-abf5-4a7e-adea-fff3ade71acc', 15, 25, 180.0);
INSERT INTO catalog.meal_template_entries VALUES (66, 'urn:musclecuties:seed:v1:c9097b2d-d068-4703-8702-2936b2858f78', 15, 48, 90.0);
INSERT INTO catalog.meal_template_entries VALUES (67, 'urn:musclecuties:seed:v1:8611fbc8-437c-47e5-a0da-5fc9a09d0c1b', 15, 27, 16.0);
INSERT INTO catalog.meal_template_entries VALUES (68, 'urn:musclecuties:seed:v1:815893ae-36e4-443a-a6ad-7600d0a36a51', 15, 63, 8.0);
INSERT INTO catalog.meal_template_entries VALUES (69, 'urn:musclecuties:seed:v1:45350c0d-b650-43b6-929d-bd710424a688', 16, 22, 180.0);
INSERT INTO catalog.meal_template_entries VALUES (70, 'urn:musclecuties:seed:v1:298bbc16-9e0c-4c85-adf1-f3bb44fbab48', 16, 30, 70.0);
INSERT INTO catalog.meal_template_entries VALUES (71, 'urn:musclecuties:seed:v1:44cc1720-7519-4318-b473-8943bb394ed2', 16, 7, 80.0);
INSERT INTO catalog.meal_template_entries VALUES (72, 'urn:musclecuties:seed:v1:dedb9e52-3386-4ec4-af53-cba19ebe175c', 16, 46, 100.0);
INSERT INTO catalog.meal_template_entries VALUES (73, 'urn:musclecuties:seed:v1:095aefd9-c03c-4e3d-84c0-80e45ad67903', 16, 2, 6.0);
INSERT INTO catalog.meal_template_entries VALUES (74, 'urn:musclecuties:seed:v1:e5c7630c-ae9a-4a5a-85a5-b6846b612540', 17, 6, 150.0);
INSERT INTO catalog.meal_template_entries VALUES (75, 'urn:musclecuties:seed:v1:5df70727-636f-4cc8-8414-f6c75c24233c', 17, 33, 180.0);
INSERT INTO catalog.meal_template_entries VALUES (76, 'urn:musclecuties:seed:v1:673c937d-9a6c-45b3-b35b-69f8afeca579', 17, 21, 70.0);
INSERT INTO catalog.meal_template_entries VALUES (77, 'urn:musclecuties:seed:v1:65fdd081-1dd2-4722-9493-22aaecf85c88', 17, 56, 45.0);


--
-- Data for Name: quiz_questions; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.quiz_questions VALUES (1, 'urn:musclecuties:seed:v1:04fdd745-0da2-42fd-bc68-0f52b1e60486', 'Current cycle phase?', 1, 'CurrentCyclePhase');
INSERT INTO catalog.quiz_questions VALUES (2, 'urn:musclecuties:seed:v1:5542a892-842d-47ed-a1d6-355763d9e8b9', 'Main fitness goal?', 2, 'Goal');
INSERT INTO catalog.quiz_questions VALUES (3, 'urn:musclecuties:seed:v1:35431b64-49fb-485e-9875-b3b4195d018c', 'How fast do you want to progress?', 3, 'GoalPace');
INSERT INTO catalog.quiz_questions VALUES (4, 'urn:musclecuties:seed:v1:477c26c3-3365-4376-b5d1-9f32c6977fb2', 'Training experience?', 4, 'ExperienceLevel');
INSERT INTO catalog.quiz_questions VALUES (5, 'urn:musclecuties:seed:v1:f8ca7c8e-e348-4506-9f0a-981d2baa8a32', 'Training days per week?', 5, 'WorkoutDaysPerWeek');
INSERT INTO catalog.quiz_questions VALUES (6, 'urn:musclecuties:seed:v1:35498be7-d02e-455b-9b3b-5d712010bf26', 'Dietary preference?', 6, 'DietaryPreference');
INSERT INTO catalog.quiz_questions VALUES (7, 'urn:musclecuties:seed:v1:1d8fe0cc-6093-4c6d-8f0e-301ab759bef6', 'Session length?', 7, 'SessionDuration');
INSERT INTO catalog.quiz_questions VALUES (8, 'urn:musclecuties:seed:v1:2b39ef76-66f0-42eb-8090-38602690c9e9', 'Available equipment?', 8, 'Equipment');
INSERT INTO catalog.quiz_questions VALUES (9, 'urn:musclecuties:seed:v1:9f34545f-ef3d-42e4-90cb-6462face3f51', 'Period discomfort?', 9, 'MenstrualPain');
INSERT INTO catalog.quiz_questions VALUES (10, 'urn:musclecuties:seed:v1:4111b082-f329-46bb-a6c3-12e6f5dab805', 'Follicular discomfort?', 11, 'FollicularPain');
INSERT INTO catalog.quiz_questions VALUES (11, 'urn:musclecuties:seed:v1:bdd13598-a16d-414d-845b-9da0ee774506', 'Ovulation discomfort?', 13, 'OvulatoryPain');
INSERT INTO catalog.quiz_questions VALUES (12, 'urn:musclecuties:seed:v1:f829cbc0-c476-496b-bf83-26c16296c246', 'Luteal symptoms?', 15, 'LutealPain');
INSERT INTO catalog.quiz_questions VALUES (13, 'urn:musclecuties:seed:v1:c98ed630-1f1f-4569-9ee6-e0cb4f49a7e3', 'Period training energy?', 10, 'MenstrualEnergy');
INSERT INTO catalog.quiz_questions VALUES (14, 'urn:musclecuties:seed:v1:dd4c044b-8b28-473e-8ac4-63845718ae6c', 'Follicular energy?', 12, 'FollicularEnergy');
INSERT INTO catalog.quiz_questions VALUES (15, 'urn:musclecuties:seed:v1:104894b7-da07-4a22-824a-86427a461a09', 'Ovulation power?', 14, 'OvulatoryEnergy');
INSERT INTO catalog.quiz_questions VALUES (16, 'urn:musclecuties:seed:v1:9d06f16c-37b1-4974-b88f-19dbbb99786e', 'Luteal training energy?', 16, 'LutealEnergy');


--
-- Data for Name: quiz_answers; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.quiz_answers VALUES (1, 'urn:musclecuties:seed:v1:71d98fa7-9e4b-486f-b48b-d8e7b700a94d', 1, 'Menstrual', 1, 0);
INSERT INTO catalog.quiz_answers VALUES (2, 'urn:musclecuties:seed:v1:83a1b8e8-08c1-4ede-92e0-f2ae50bf9dee', 1, 'Follicular', 2, 1);
INSERT INTO catalog.quiz_answers VALUES (3, 'urn:musclecuties:seed:v1:2141eb32-b279-463a-8d21-6b9816d3673d', 1, 'Ovulatory', 3, 2);
INSERT INTO catalog.quiz_answers VALUES (4, 'urn:musclecuties:seed:v1:d1a41867-d12e-4114-a6da-2f3be317604d', 1, 'Luteal', 4, 3);
INSERT INTO catalog.quiz_answers VALUES (5, 'urn:musclecuties:seed:v1:8866d341-ad5d-45f1-9817-9c05ac79624b', 2, 'Lose fat', 1, 0);
INSERT INTO catalog.quiz_answers VALUES (6, 'urn:musclecuties:seed:v1:36e23ce1-9ec2-4774-9ff8-945fb60015c4', 2, 'Build muscle tone', 2, 1);
INSERT INTO catalog.quiz_answers VALUES (7, 'urn:musclecuties:seed:v1:d05a454f-cab0-40c8-9441-7a6b9ba7f10d', 2, 'Get stronger', 3, 2);
INSERT INTO catalog.quiz_answers VALUES (8, 'urn:musclecuties:seed:v1:78a97329-3374-468e-9492-9d98da43d36c', 2, 'Maintain health', 4, 3);
INSERT INTO catalog.quiz_answers VALUES (9, 'urn:musclecuties:seed:v1:0b348e29-05f5-4d1d-aefb-e0287f556e1e', 3, 'Steady', 1, 0);
INSERT INTO catalog.quiz_answers VALUES (10, 'urn:musclecuties:seed:v1:554c1ca6-25e9-49c3-ac5d-e4edbb1a60ac', 3, 'Aggressive', 2, 1);
INSERT INTO catalog.quiz_answers VALUES (11, 'urn:musclecuties:seed:v1:17c5fb99-3c33-4f34-8d46-6c3aa4aa6afd', 4, 'Beginner', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (12, 'urn:musclecuties:seed:v1:84c3fe3a-ec58-496f-8b1e-415d3644e22c', 4, 'Intermediate', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (13, 'urn:musclecuties:seed:v1:014683e6-064b-400d-b3c2-a9922df7615c', 4, 'Advanced', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (14, 'urn:musclecuties:seed:v1:e435cff3-a7f9-464d-bb20-763a19808422', 5, '2 days', 1, 2);
INSERT INTO catalog.quiz_answers VALUES (15, 'urn:musclecuties:seed:v1:0f90563c-0a71-4add-b3f7-ceae61a656e3', 5, '3 days', 2, 3);
INSERT INTO catalog.quiz_answers VALUES (16, 'urn:musclecuties:seed:v1:256df14e-0e50-4108-8ee1-e3431a6a13c9', 5, '4 days', 3, 4);
INSERT INTO catalog.quiz_answers VALUES (17, 'urn:musclecuties:seed:v1:1a3b6d14-66af-485d-a7b9-11f1c1b1ea45', 5, '5 days', 4, 5);
INSERT INTO catalog.quiz_answers VALUES (18, 'urn:musclecuties:seed:v1:e70a7af8-fcf3-4817-abf8-b5bacbbc424b', 6, 'No preference', 1, 0);
INSERT INTO catalog.quiz_answers VALUES (19, 'urn:musclecuties:seed:v1:50a1d794-56f5-4eb3-9f7d-b7f0487397d2', 6, 'Vegetarian', 2, 1);
INSERT INTO catalog.quiz_answers VALUES (20, 'urn:musclecuties:seed:v1:1b74843b-bb04-4213-abd0-9c366798f7f2', 6, 'Vegan', 3, 2);
INSERT INTO catalog.quiz_answers VALUES (21, 'urn:musclecuties:seed:v1:4c296e93-099f-4841-aa07-85ee7061a686', 6, 'Gluten-free', 4, 3);
INSERT INTO catalog.quiz_answers VALUES (22, 'urn:musclecuties:seed:v1:1d33efb2-6291-4f51-9024-b9de607ed547', 6, 'Lactose-free', 5, 4);
INSERT INTO catalog.quiz_answers VALUES (23, 'urn:musclecuties:seed:v1:c7420e5a-5f21-41ef-a226-64a95dd7dee0', 7, '30 min', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (24, 'urn:musclecuties:seed:v1:540f1e1d-ad89-4acd-bd21-66e8745efe63', 7, '45 min', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (25, 'urn:musclecuties:seed:v1:81c031df-3136-4784-a26c-47681ef8f468', 7, '60 min', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (26, 'urn:musclecuties:seed:v1:eb9b1db7-a00f-4c71-85cc-dc7a7453e7b3', 7, '75 min', 4, 4);
INSERT INTO catalog.quiz_answers VALUES (27, 'urn:musclecuties:seed:v1:544fe426-148e-4819-bcb5-292e9d665e18', 7, '90 min', 5, 5);
INSERT INTO catalog.quiz_answers VALUES (28, 'urn:musclecuties:seed:v1:9c499103-3094-4a8e-923d-21532fccd38d', 8, 'Full gym', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (29, 'urn:musclecuties:seed:v1:1a055b83-e11e-4997-822a-660d72d15f58', 8, 'Dumbbells and bands', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (30, 'urn:musclecuties:seed:v1:2c50cf03-f689-4e36-ace3-770ce3c924eb', 8, 'Bodyweight only', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (31, 'urn:musclecuties:seed:v1:82a2ccc5-71b3-4198-950c-91788a510c7f', 9, 'Barely there', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (32, 'urn:musclecuties:seed:v1:72dc4abf-393d-4e13-9d3c-792bff683453', 9, 'Manageable', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (33, 'urn:musclecuties:seed:v1:da6776b9-3049-4d48-a4fa-fb01cadc7e55', 9, 'Noticeable', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (34, 'urn:musclecuties:seed:v1:9c6f1fb3-3bb8-4e85-9249-05fbe080dc58', 9, 'Rough', 4, 4);
INSERT INTO catalog.quiz_answers VALUES (35, 'urn:musclecuties:seed:v1:30da180f-e31a-4cb6-ad85-1083316ea6c6', 9, 'Stops my day', 5, 5);
INSERT INTO catalog.quiz_answers VALUES (36, 'urn:musclecuties:seed:v1:5f19e360-b223-4467-822b-d65f0acca94e', 10, 'Barely there', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (37, 'urn:musclecuties:seed:v1:3f3b5c9e-5e54-4f2c-b22c-be77db2bcc28', 10, 'Manageable', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (38, 'urn:musclecuties:seed:v1:1789917b-20de-4bc1-973f-6d33d8e98c77', 10, 'Noticeable', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (39, 'urn:musclecuties:seed:v1:31564d7a-edc4-4c03-ba23-6ecb6dcb1217', 10, 'Rough', 4, 4);
INSERT INTO catalog.quiz_answers VALUES (40, 'urn:musclecuties:seed:v1:65cb4ae6-e74f-4fd7-bae9-1e5d1ad6a88f', 10, 'Stops my day', 5, 5);
INSERT INTO catalog.quiz_answers VALUES (41, 'urn:musclecuties:seed:v1:662a71d7-6b46-4d2f-9e0a-9b0756aeff5f', 11, 'Barely there', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (42, 'urn:musclecuties:seed:v1:51048319-353d-4e52-9d8f-5edb622fd7c0', 11, 'Manageable', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (43, 'urn:musclecuties:seed:v1:6a5bbf6d-9936-4b3c-8790-842d668ae8a7', 11, 'Noticeable', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (44, 'urn:musclecuties:seed:v1:e8d524d9-d31e-488f-9af6-8984d5c97910', 11, 'Rough', 4, 4);
INSERT INTO catalog.quiz_answers VALUES (45, 'urn:musclecuties:seed:v1:45fc337f-5d7c-4971-b7b1-fbb9f2053512', 11, 'Stops my day', 5, 5);
INSERT INTO catalog.quiz_answers VALUES (46, 'urn:musclecuties:seed:v1:e257aba2-0c6e-429f-86de-47338e388d25', 12, 'Barely there', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (47, 'urn:musclecuties:seed:v1:b4abbcd0-5532-428f-8eef-859f93cb3b40', 12, 'Manageable', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (48, 'urn:musclecuties:seed:v1:e0167e3c-bf0a-4395-90d1-b2288b461225', 12, 'Noticeable', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (49, 'urn:musclecuties:seed:v1:62940104-01bd-416c-8cb7-4730fefc8130', 12, 'Rough', 4, 4);
INSERT INTO catalog.quiz_answers VALUES (50, 'urn:musclecuties:seed:v1:ee9e5a99-045c-421a-a13f-cbd5fc0a6641', 12, 'Stops my day', 5, 5);
INSERT INTO catalog.quiz_answers VALUES (51, 'urn:musclecuties:seed:v1:6691b666-1c2f-46cd-8b74-b9ce2b5c6907', 13, 'Couch-level', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (52, 'urn:musclecuties:seed:v1:7950a3c4-f8b8-4be2-87f0-928940fe1ca8', 13, 'Slow but moving', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (53, 'urn:musclecuties:seed:v1:71afeb6e-7d93-449c-a49d-218b6111be5b', 13, 'Steady', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (54, 'urn:musclecuties:seed:v1:d3f34e7f-0631-4a37-9da7-8748e0fd98b7', 13, 'Strong', 4, 4);
INSERT INTO catalog.quiz_answers VALUES (55, 'urn:musclecuties:seed:v1:4c4be9b6-b3b6-44de-bd60-2919b721be8e', 13, 'Ready to push', 5, 5);
INSERT INTO catalog.quiz_answers VALUES (56, 'urn:musclecuties:seed:v1:a07e2f9e-e307-4a3f-9e1a-ec57352951ba', 14, 'Couch-level', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (57, 'urn:musclecuties:seed:v1:685f3dad-b135-42aa-86a0-2dbf2c477bed', 14, 'Slow but moving', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (58, 'urn:musclecuties:seed:v1:cb6fdef9-42f5-41ef-806b-3b80c756a52e', 14, 'Steady', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (59, 'urn:musclecuties:seed:v1:633a2398-6afa-4247-8b4c-540a3f1b1db6', 14, 'Strong', 4, 4);
INSERT INTO catalog.quiz_answers VALUES (60, 'urn:musclecuties:seed:v1:bec82b78-ee43-41ad-86ad-163948e47b69', 14, 'Ready to push', 5, 5);
INSERT INTO catalog.quiz_answers VALUES (61, 'urn:musclecuties:seed:v1:3a4b22fe-143e-4d79-af40-b8fe6a29ae65', 15, 'Couch-level', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (62, 'urn:musclecuties:seed:v1:721b271f-8e7d-41ab-9525-c144385ed454', 15, 'Slow but moving', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (63, 'urn:musclecuties:seed:v1:160b8b8c-7959-4a09-974f-9968e85cfda1', 15, 'Steady', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (64, 'urn:musclecuties:seed:v1:8146f8fa-d65a-4a90-9ed5-0f77b34d1611', 15, 'Strong', 4, 4);
INSERT INTO catalog.quiz_answers VALUES (65, 'urn:musclecuties:seed:v1:738792ab-16e7-42ad-a80a-44b716eff980', 15, 'Ready to push', 5, 5);
INSERT INTO catalog.quiz_answers VALUES (66, 'urn:musclecuties:seed:v1:cdfb4aed-99ce-4e01-bd02-e0cecab2edd4', 16, 'Couch-level', 1, 1);
INSERT INTO catalog.quiz_answers VALUES (67, 'urn:musclecuties:seed:v1:17e9a7e2-679c-4b58-b373-9c23dcd54594', 16, 'Slow but moving', 2, 2);
INSERT INTO catalog.quiz_answers VALUES (68, 'urn:musclecuties:seed:v1:4f346c28-3822-4931-9718-df111cfb163e', 16, 'Steady', 3, 3);
INSERT INTO catalog.quiz_answers VALUES (69, 'urn:musclecuties:seed:v1:7d7c1655-dd6d-4b59-86a2-a52a3fbdc375', 16, 'Strong', 4, 4);
INSERT INTO catalog.quiz_answers VALUES (70, 'urn:musclecuties:seed:v1:2b3a6a99-cf9d-40ae-a416-0b244042d7cc', 16, 'Ready to push', 5, 5);


--
-- Data for Name: session_archetypes; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.session_archetypes VALUES (1, 'urn:musclecuties:seed:v1:c25893ae-dae5-42e3-abca-7b23d1fa0d76', 'P', 'Posterior / Thrust', true, true);
INSERT INTO catalog.session_archetypes VALUES (2, 'urn:musclecuties:seed:v1:4e45b4d3-e171-453f-b6dc-826ddba84e15', 'S', 'Squat / Abduction', true, false);
INSERT INTO catalog.session_archetypes VALUES (3, 'urn:musclecuties:seed:v1:a42fb3c4-ba21-40df-a898-d005a1c9a6d1', 'U', 'Upper (pull emphasis)', false, false);
INSERT INTO catalog.session_archetypes VALUES (4, 'urn:musclecuties:seed:v1:05e19187-c8b4-4453-9573-bc30f5fa3f2f', 'U2', 'Upper (push emphasis)', false, false);
INSERT INTO catalog.session_archetypes VALUES (5, 'urn:musclecuties:seed:v1:47c03782-db43-46d9-b7f6-06cbfac94064', 'F', 'Full-body glute-biased', true, true);
INSERT INTO catalog.session_archetypes VALUES (6, 'urn:musclecuties:seed:v1:c4c9a8f7-ab4b-4734-8062-c76bee04951b', 'G', 'Glute / Abductor short', true, false);
INSERT INTO catalog.session_archetypes VALUES (7, 'urn:musclecuties:seed:v1:1e0dff36-2b52-48e8-9757-5206933c26ae', 'C', 'Climbing', false, false);


--
-- Data for Name: slot_templates; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.slot_templates VALUES (1, 'urn:musclecuties:seed:v1:ce57897a-b64c-4d80-881d-86b2a489762a', 1, 1, 'HighIntensity', 'HipThrust', 1, 2, 3, 5, 7, 1, false, 0);
INSERT INTO catalog.slot_templates VALUES (2, 'urn:musclecuties:seed:v1:45c39606-d119-420a-9236-6131a9d6bf7a', 1, 2, 'Hypertrophy', 'HipHinge', 2, 3, 4, 8, 10, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (3, 'urn:musclecuties:seed:v1:53a041e3-83de-4788-aab1-f8c5ee1b9f0c', 1, 3, 'Hypertrophy', 'HipThrust,HipHinge', 1, 3, 4, 10, 12, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (4, 'urn:musclecuties:seed:v1:cddb6a7f-541c-4af8-8ae6-8d332ff9c201', 1, 4, 'Accessory', 'KneeFlexion', 2, 2, 3, 10, 15, 1, true, 1);
INSERT INTO catalog.slot_templates VALUES (5, 'urn:musclecuties:seed:v1:c2558d66-74a6-4ba3-859e-6c1ebd74e4fb', 1, 5, 'Accessory', 'HipAbduction', 3, 2, 3, 15, 20, 1, false, 1);
INSERT INTO catalog.slot_templates VALUES (6, 'urn:musclecuties:seed:v1:9027a047-f38c-4cb7-9290-2e9c10978bf9', 1, 6, 'Core', 'AntiExtension', 12, 2, 3, 8, 15, 2, true, 2);
INSERT INTO catalog.slot_templates VALUES (7, 'urn:musclecuties:seed:v1:83fa5b3f-7fdf-472a-af33-9c181aa3e2e5', 2, 1, 'HighIntensity', 'SquatPattern', 5, 2, 3, 5, 7, 1, false, 0);
INSERT INTO catalog.slot_templates VALUES (8, 'urn:musclecuties:seed:v1:5bf2853a-702f-4427-a56e-79aec791390c', 2, 2, 'Hypertrophy', 'Lunge,SquatPattern', 1, 3, 4, 8, 12, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (9, 'urn:musclecuties:seed:v1:82cc9a75-fe81-4784-b337-7e76271dd8d8', 2, 3, 'Hypertrophy', 'HipHinge', 1, 3, 4, 10, 15, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (10, 'urn:musclecuties:seed:v1:b353d7b1-4578-497e-9a88-80e6883ede14', 2, 4, 'Accessory', 'HipAbduction', 3, 3, 3, 15, 20, 1, false, 1);
INSERT INTO catalog.slot_templates VALUES (11, 'urn:musclecuties:seed:v1:1d367968-1f19-4167-a063-cbe9c0d63203', 2, 5, 'Accessory', 'HipAdduction,CalfRaise', 4, 2, 2, 12, 20, 2, true, 1);
INSERT INTO catalog.slot_templates VALUES (12, 'urn:musclecuties:seed:v1:1ec11c1c-37e8-4330-9937-228963ab5690', 2, 6, 'Core', 'AntiRotation', 12, 2, 3, 8, 12, 2, true, 2);
INSERT INTO catalog.slot_templates VALUES (13, 'urn:musclecuties:seed:v1:c169d336-a287-4a7c-8edc-ea8f44d05971', 3, 1, 'HighIntensity', 'VerticalPull', 6, 2, 3, 5, 7, 1, false, 0);
INSERT INTO catalog.slot_templates VALUES (14, 'urn:musclecuties:seed:v1:7bff43ed-7e25-403c-9e40-70b3ef3e014e', 3, 2, 'Hypertrophy', 'HorizontalPull', 7, 3, 4, 8, 12, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (15, 'urn:musclecuties:seed:v1:1d642eae-e215-459f-9de4-40ca0e03973e', 3, 3, 'Hypertrophy', 'VerticalPush,HorizontalPush', 8, 3, 3, 8, 12, 2, true, 0);
INSERT INTO catalog.slot_templates VALUES (16, 'urn:musclecuties:seed:v1:927a9e8f-c704-4f80-9e2e-a748441c68a2', 3, 4, 'Accessory', 'LateralRaise', 8, 3, 3, 12, 20, 1, false, 1);
INSERT INTO catalog.slot_templates VALUES (17, 'urn:musclecuties:seed:v1:8fe81c43-afb6-4d2c-983f-fd49aa5bf758', 3, 5, 'Accessory', 'ElbowFlexion', 10, 2, 3, 10, 15, 1, true, 1);
INSERT INTO catalog.slot_templates VALUES (18, 'urn:musclecuties:seed:v1:051b0c09-aa64-4573-9501-903a9b32d6c6', 3, 6, 'Core', 'AntiRotation,AntiExtension', 12, 2, 2, 8, 15, 2, true, 2);
INSERT INTO catalog.slot_templates VALUES (19, 'urn:musclecuties:seed:v1:ce5f4c34-5ae3-420f-9b9f-027dab774969', 4, 1, 'HighIntensity', 'VerticalPush,HorizontalPush', 8, 2, 3, 5, 7, 1, false, 0);
INSERT INTO catalog.slot_templates VALUES (20, 'urn:musclecuties:seed:v1:9d147926-2259-45d5-a201-fa23eea46be9', 4, 2, 'Hypertrophy', 'HorizontalPull', 7, 3, 4, 8, 12, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (21, 'urn:musclecuties:seed:v1:f0966dfa-cfc5-44eb-ae94-615265e4358f', 4, 3, 'Hypertrophy', 'VerticalPull', 6, 3, 3, 10, 12, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (22, 'urn:musclecuties:seed:v1:8083b04a-4a33-460a-b932-b728dc7ae2c5', 4, 4, 'Accessory', 'LateralRaise,RearDelt', 8, 3, 3, 12, 20, 1, false, 1);
INSERT INTO catalog.slot_templates VALUES (23, 'urn:musclecuties:seed:v1:77f38f91-8b52-45e2-ac56-aa0965459c4d', 4, 5, 'Accessory', 'ElbowExtension', 11, 2, 3, 10, 15, 1, true, 1);
INSERT INTO catalog.slot_templates VALUES (24, 'urn:musclecuties:seed:v1:385ebeba-905b-4496-b70c-d97983820b2c', 4, 6, 'Core', 'AntiExtension', 12, 2, 2, 8, 15, 2, true, 2);
INSERT INTO catalog.slot_templates VALUES (25, 'urn:musclecuties:seed:v1:6b53fb87-faba-49c8-b2d2-fa9c2b6aa9ec', 5, 1, 'HighIntensity', 'HipThrust', 1, 2, 3, 5, 7, 1, false, 0);
INSERT INTO catalog.slot_templates VALUES (26, 'urn:musclecuties:seed:v1:17c1eea9-97a7-4e22-b14b-3c4582524b21', 5, 2, 'Hypertrophy', 'HipHinge', 2, 3, 4, 8, 10, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (27, 'urn:musclecuties:seed:v1:11ae2a90-33cf-429f-8763-c8abe9df988f', 5, 3, 'Hypertrophy', 'VerticalPull,HorizontalPull', 6, 3, 4, 8, 12, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (28, 'urn:musclecuties:seed:v1:d9e1c37f-dcc4-4a0d-a052-b3e824daabd2', 5, 4, 'Hypertrophy', 'SquatPattern,Lunge', 1, 3, 3, 10, 12, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (29, 'urn:musclecuties:seed:v1:4c7d6569-bdbe-43c2-a9fb-3d8ccbc11cc0', 5, 5, 'Accessory', 'HipAbduction', 3, 2, 3, 15, 20, 1, false, 1);
INSERT INTO catalog.slot_templates VALUES (30, 'urn:musclecuties:seed:v1:36b9126c-b7fe-4731-a715-6d7508af1cc9', 5, 6, 'Accessory', 'VerticalPush,LateralRaise', 8, 2, 3, 10, 15, 1, true, 1);
INSERT INTO catalog.slot_templates VALUES (31, 'urn:musclecuties:seed:v1:19d674f2-6a6a-4e8c-9921-24938e787aff', 5, 7, 'Core', 'AntiExtension,AntiRotation', 12, 2, 3, 8, 15, 2, true, 2);
INSERT INTO catalog.slot_templates VALUES (32, 'urn:musclecuties:seed:v1:971ac831-eabb-44b6-b618-07bbcd310f4c', 5, 8, 'Accessory', 'HorizontalPush', 9, 2, 2, 10, 15, 2, true, 3);
INSERT INTO catalog.slot_templates VALUES (33, 'urn:musclecuties:seed:v1:87e5cf4e-9838-440a-ae3c-16bbafe6f615', 5, 9, 'Accessory', 'CalfRaise', 13, 2, 2, 12, 20, 2, true, 3);
INSERT INTO catalog.slot_templates VALUES (34, 'urn:musclecuties:seed:v1:6c76bd2e-4621-4223-9bf6-f19116a065f3', 6, 1, 'Hypertrophy', 'HipThrust', 1, 3, 4, 10, 12, 2, false, 0);
INSERT INTO catalog.slot_templates VALUES (35, 'urn:musclecuties:seed:v1:be6133a7-ba94-48b2-879e-162f215d342b', 6, 2, 'Hypertrophy', 'HipHinge', 1, 3, 3, 10, 15, 2, false, 1);
INSERT INTO catalog.slot_templates VALUES (36, 'urn:musclecuties:seed:v1:c9b0c573-8f3c-4ee5-bde6-5bb1ad3aaec2', 6, 3, 'Accessory', 'HipAbduction', 3, 3, 3, 15, 20, 1, false, 1);
INSERT INTO catalog.slot_templates VALUES (37, 'urn:musclecuties:seed:v1:19f6612b-d698-466c-a7cb-8b2fe9a1d807', 6, 4, 'Accessory', 'HipAbduction', 3, 2, 2, 20, 25, 1, true, 2);
INSERT INTO catalog.slot_templates VALUES (38, 'urn:musclecuties:seed:v1:de2d3798-23bd-4c5b-a745-d49cff38bef5', 7, 1, 'Hypertrophy', 'Climbing', 6, 1, 1, 0, 0, 0, false, 0);


--
-- Data for Name: volume_budget_rows; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.volume_budget_rows VALUES (1, 'urn:musclecuties:seed:v1:f2156d94-4e35-47b3-85a3-32fbea6f65fa', 2, 1, 11.0);
INSERT INTO catalog.volume_budget_rows VALUES (2, 'urn:musclecuties:seed:v1:a487ec28-c8b0-48a0-afa4-a9078cec4bf5', 3, 1, 14.0);
INSERT INTO catalog.volume_budget_rows VALUES (3, 'urn:musclecuties:seed:v1:1c37abd2-f276-4161-8592-43059b01a1fd', 4, 1, 18.0);
INSERT INTO catalog.volume_budget_rows VALUES (4, 'urn:musclecuties:seed:v1:47cd2584-38a1-44a2-88dd-15a59932d2b9', 5, 1, 21.0);
INSERT INTO catalog.volume_budget_rows VALUES (5, 'urn:musclecuties:seed:v1:fff953d4-1878-4570-9ce7-da9a75c93b75', 6, 1, 24.0);
INSERT INTO catalog.volume_budget_rows VALUES (6, 'urn:musclecuties:seed:v1:41cc7f6a-c782-4050-b2e7-49a0a9abcd8b', 2, 2, 7.0);
INSERT INTO catalog.volume_budget_rows VALUES (7, 'urn:musclecuties:seed:v1:1167b603-cf58-454a-8b13-0ff20052b85a', 3, 2, 9.0);
INSERT INTO catalog.volume_budget_rows VALUES (8, 'urn:musclecuties:seed:v1:ef232b08-0e1e-4935-8492-f7912e24a7d9', 4, 2, 12.0);
INSERT INTO catalog.volume_budget_rows VALUES (9, 'urn:musclecuties:seed:v1:f4936266-a192-40c4-9ce0-40febca372fe', 5, 2, 14.0);
INSERT INTO catalog.volume_budget_rows VALUES (10, 'urn:musclecuties:seed:v1:ac700894-be78-4c5e-83b1-6d8ed987155c', 6, 2, 15.0);
INSERT INTO catalog.volume_budget_rows VALUES (11, 'urn:musclecuties:seed:v1:72483b4c-e56f-4932-a1fe-305640a98ff8', 2, 3, 5.0);
INSERT INTO catalog.volume_budget_rows VALUES (12, 'urn:musclecuties:seed:v1:56d25cf5-2e01-4587-93ac-58d20c668230', 3, 3, 6.0);
INSERT INTO catalog.volume_budget_rows VALUES (13, 'urn:musclecuties:seed:v1:48619d81-58d2-4281-ba91-c967790276da', 4, 3, 8.0);
INSERT INTO catalog.volume_budget_rows VALUES (14, 'urn:musclecuties:seed:v1:b6a63fa3-a374-468f-861c-293dc10e082a', 5, 3, 10.0);
INSERT INTO catalog.volume_budget_rows VALUES (15, 'urn:musclecuties:seed:v1:8587a379-5647-4088-b472-6887683d6af9', 6, 3, 10.0);
INSERT INTO catalog.volume_budget_rows VALUES (16, 'urn:musclecuties:seed:v1:39c0fb92-7665-4f8b-ad21-324a390788ac', 2, 6, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (17, 'urn:musclecuties:seed:v1:9662fbe0-ada7-4480-81d7-6ecdb015d505', 3, 6, 5.0);
INSERT INTO catalog.volume_budget_rows VALUES (18, 'urn:musclecuties:seed:v1:091c3977-a688-461d-be18-85b889a945fa', 4, 6, 7.0);
INSERT INTO catalog.volume_budget_rows VALUES (19, 'urn:musclecuties:seed:v1:5f8784d0-b8ae-45e7-a440-1b70a35aa4a1', 5, 6, 8.0);
INSERT INTO catalog.volume_budget_rows VALUES (20, 'urn:musclecuties:seed:v1:d3f363b0-0e0c-4f37-b45d-75420d930cf3', 6, 6, 8.0);
INSERT INTO catalog.volume_budget_rows VALUES (21, 'urn:musclecuties:seed:v1:c969ca90-540b-43f2-9206-04b080831bf9', 2, 7, 3.0);
INSERT INTO catalog.volume_budget_rows VALUES (22, 'urn:musclecuties:seed:v1:161ff3f4-c5c2-400a-b7df-6bced77cb31b', 3, 7, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (23, 'urn:musclecuties:seed:v1:ccc03c48-3af3-40c8-8d2e-9010b6548c49', 4, 7, 5.0);
INSERT INTO catalog.volume_budget_rows VALUES (24, 'urn:musclecuties:seed:v1:10ea5d0d-515b-47da-b386-6a464276706b', 5, 7, 6.0);
INSERT INTO catalog.volume_budget_rows VALUES (25, 'urn:musclecuties:seed:v1:37860966-ade9-460a-8ec2-6385640d54ea', 6, 7, 7.0);
INSERT INTO catalog.volume_budget_rows VALUES (26, 'urn:musclecuties:seed:v1:164fd6dd-fbec-4a34-99dd-0fd2a1ea0ac6', 2, 8, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (27, 'urn:musclecuties:seed:v1:4df77f5d-3a0b-474a-9930-eb45a1fa1a0c', 3, 8, 6.0);
INSERT INTO catalog.volume_budget_rows VALUES (28, 'urn:musclecuties:seed:v1:fc84cef6-2606-4dc7-b12a-6e90abc294c7', 4, 8, 8.0);
INSERT INTO catalog.volume_budget_rows VALUES (29, 'urn:musclecuties:seed:v1:dcdfec22-a715-424a-9446-13dac8f0e99f', 5, 8, 10.0);
INSERT INTO catalog.volume_budget_rows VALUES (30, 'urn:musclecuties:seed:v1:a03059be-8791-4e7a-b4be-423fe6e1c320', 6, 8, 10.0);
INSERT INTO catalog.volume_budget_rows VALUES (31, 'urn:musclecuties:seed:v1:6be7e70d-2c09-4a05-8b71-b460cb6523b5', 2, 12, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (32, 'urn:musclecuties:seed:v1:b77cad98-24a3-486f-8f83-18d4aaf6ce41', 3, 12, 6.0);
INSERT INTO catalog.volume_budget_rows VALUES (33, 'urn:musclecuties:seed:v1:6b67755a-35da-4e79-9a3e-885dcdf7720c', 4, 12, 8.0);
INSERT INTO catalog.volume_budget_rows VALUES (34, 'urn:musclecuties:seed:v1:292a98fb-6fdb-4297-9c29-6de3ee0d0075', 5, 12, 9.0);
INSERT INTO catalog.volume_budget_rows VALUES (35, 'urn:musclecuties:seed:v1:1f555569-6110-48dc-8dc9-a343a5a4998f', 6, 12, 10.0);
INSERT INTO catalog.volume_budget_rows VALUES (36, 'urn:musclecuties:seed:v1:aacc9204-e665-4d6f-b8b1-66b80b30a5dc', 2, 10, 1.5);
INSERT INTO catalog.volume_budget_rows VALUES (37, 'urn:musclecuties:seed:v1:1b35542f-9100-43be-a864-e67e02abc56d', 3, 10, 2.5);
INSERT INTO catalog.volume_budget_rows VALUES (38, 'urn:musclecuties:seed:v1:2386d5be-8253-466b-b0ee-164b70b4fa50', 4, 10, 3.5);
INSERT INTO catalog.volume_budget_rows VALUES (39, 'urn:musclecuties:seed:v1:67334d19-404d-41cb-9f27-f712ffb32341', 5, 10, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (40, 'urn:musclecuties:seed:v1:b44e5f4f-50c8-4c16-8683-473570b2a692', 6, 10, 4.5);
INSERT INTO catalog.volume_budget_rows VALUES (41, 'urn:musclecuties:seed:v1:7495153e-1884-4a06-a25f-46046e4bf618', 2, 11, 1.5);
INSERT INTO catalog.volume_budget_rows VALUES (42, 'urn:musclecuties:seed:v1:65230130-efdd-4982-a779-8782969663d4', 3, 11, 2.5);
INSERT INTO catalog.volume_budget_rows VALUES (43, 'urn:musclecuties:seed:v1:5e7eb0f5-0e1e-4ac1-91bb-dd364dd8a932', 4, 11, 3.5);
INSERT INTO catalog.volume_budget_rows VALUES (44, 'urn:musclecuties:seed:v1:1c065ac7-fde5-4487-a3cc-5217072631b0', 5, 11, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (45, 'urn:musclecuties:seed:v1:03eb6b1b-1426-4360-aa41-3d177cd7854c', 6, 11, 4.5);
INSERT INTO catalog.volume_budget_rows VALUES (46, 'urn:musclecuties:seed:v1:1ce98b04-ee51-4607-9ab5-adbef48600b1', 2, 5, 5.0);
INSERT INTO catalog.volume_budget_rows VALUES (47, 'urn:musclecuties:seed:v1:6cc94406-c1eb-47a0-bc82-63e7ceba6fc3', 3, 5, 6.0);
INSERT INTO catalog.volume_budget_rows VALUES (48, 'urn:musclecuties:seed:v1:73001199-a326-4aa9-adc7-3c3b579832c7', 4, 5, 8.0);
INSERT INTO catalog.volume_budget_rows VALUES (49, 'urn:musclecuties:seed:v1:a202b30b-8e19-406d-b17f-48a2c29ac40a', 5, 5, 9.0);
INSERT INTO catalog.volume_budget_rows VALUES (50, 'urn:musclecuties:seed:v1:5f5e7f1e-6069-4823-ac18-6e1dbff15a29', 6, 5, 10.0);
INSERT INTO catalog.volume_budget_rows VALUES (51, 'urn:musclecuties:seed:v1:c12f3f47-18d9-4ec8-84c5-f969ea2c0dcc', 2, 4, 2.0);
INSERT INTO catalog.volume_budget_rows VALUES (52, 'urn:musclecuties:seed:v1:d47f2c45-be4a-4fd6-a86c-a6d34351170f', 3, 4, 3.0);
INSERT INTO catalog.volume_budget_rows VALUES (53, 'urn:musclecuties:seed:v1:9cd0d0cf-db05-49ee-b401-c67ced403e56', 4, 4, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (54, 'urn:musclecuties:seed:v1:82dd8842-7868-4031-95b8-ed17ae2ae86b', 5, 4, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (55, 'urn:musclecuties:seed:v1:08cc7101-f9c1-425d-9752-31fcc732f2b6', 6, 4, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (56, 'urn:musclecuties:seed:v1:9ed451b5-681e-40a9-a8be-a4c49cc4e39b', 2, 9, 2.0);
INSERT INTO catalog.volume_budget_rows VALUES (57, 'urn:musclecuties:seed:v1:31e92f59-a6da-4904-8f9e-fec207f6afd3', 3, 9, 3.0);
INSERT INTO catalog.volume_budget_rows VALUES (58, 'urn:musclecuties:seed:v1:c1786d00-e0f1-440b-b77f-1c2e41a9c2b5', 4, 9, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (59, 'urn:musclecuties:seed:v1:cd97dfab-e35a-4970-ba5e-373f6be0aee5', 5, 9, 5.0);
INSERT INTO catalog.volume_budget_rows VALUES (60, 'urn:musclecuties:seed:v1:83b8fc7c-4971-4708-aadd-d65f371801ff', 6, 9, 6.0);
INSERT INTO catalog.volume_budget_rows VALUES (61, 'urn:musclecuties:seed:v1:e8ff6c34-5299-4230-90f2-724cb12fd81e', 2, 13, 1.0);
INSERT INTO catalog.volume_budget_rows VALUES (62, 'urn:musclecuties:seed:v1:066e1907-8771-4349-8dc1-43240879a15c', 3, 13, 2.0);
INSERT INTO catalog.volume_budget_rows VALUES (63, 'urn:musclecuties:seed:v1:53f38497-ac43-488a-9747-9b1b19b19d4a', 4, 13, 3.0);
INSERT INTO catalog.volume_budget_rows VALUES (64, 'urn:musclecuties:seed:v1:4fe3699c-9b06-4580-bde8-623c87744d4b', 5, 13, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (65, 'urn:musclecuties:seed:v1:e58ba5b3-6c53-4f16-87c6-66108ca4c0e5', 6, 13, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (66, 'urn:musclecuties:seed:v1:e7be5050-b532-4bac-a6b4-f08ff9b616e0', 7, 1, 24.0);
INSERT INTO catalog.volume_budget_rows VALUES (67, 'urn:musclecuties:seed:v1:97131a97-b4e9-492b-a364-919826fca889', 7, 2, 15.0);
INSERT INTO catalog.volume_budget_rows VALUES (68, 'urn:musclecuties:seed:v1:1fe819a6-c042-42dd-b8eb-87e9ba72c632', 7, 3, 10.0);
INSERT INTO catalog.volume_budget_rows VALUES (69, 'urn:musclecuties:seed:v1:c7625873-659b-428a-9815-06f1124304f8', 7, 6, 8.0);
INSERT INTO catalog.volume_budget_rows VALUES (70, 'urn:musclecuties:seed:v1:65024a07-a186-43a3-a66c-69bfceb1f2b3', 7, 7, 7.0);
INSERT INTO catalog.volume_budget_rows VALUES (71, 'urn:musclecuties:seed:v1:059e283b-419a-471d-8829-36f1d9dbc242', 7, 8, 10.0);
INSERT INTO catalog.volume_budget_rows VALUES (72, 'urn:musclecuties:seed:v1:c92fc3a3-0c3f-4293-a0b5-6a7d5426f46d', 7, 12, 10.0);
INSERT INTO catalog.volume_budget_rows VALUES (73, 'urn:musclecuties:seed:v1:264ef6d5-d939-4a04-9e40-9a99d83c77a5', 7, 10, 4.5);
INSERT INTO catalog.volume_budget_rows VALUES (74, 'urn:musclecuties:seed:v1:ac9f31fe-8df0-490d-afa4-9e588dd68f46', 7, 11, 4.5);
INSERT INTO catalog.volume_budget_rows VALUES (75, 'urn:musclecuties:seed:v1:6fccdca7-97e9-456f-9599-24c0c0bca4f3', 7, 5, 10.0);
INSERT INTO catalog.volume_budget_rows VALUES (76, 'urn:musclecuties:seed:v1:47c1cbfc-28b8-40e0-b98b-8b70f04754ff', 7, 4, 4.0);
INSERT INTO catalog.volume_budget_rows VALUES (77, 'urn:musclecuties:seed:v1:5945d223-14c5-4755-8609-0a3ff63f4ce9', 7, 9, 6.0);
INSERT INTO catalog.volume_budget_rows VALUES (78, 'urn:musclecuties:seed:v1:4567d39c-3c0e-4038-b002-eb74e2d69fa2', 7, 13, 4.0);


--
-- Data for Name: week_templates; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.week_templates VALUES (1, 'urn:musclecuties:seed:v1:50161d55-2c63-49b5-9855-c5874044ecee', 2, '[5,5]');
INSERT INTO catalog.week_templates VALUES (2, 'urn:musclecuties:seed:v1:0a772a89-787d-4182-a630-c77eb94ecf40', 3, '[1,3,2]');
INSERT INTO catalog.week_templates VALUES (3, 'urn:musclecuties:seed:v1:b69f9bd9-841e-4b85-8d59-44c8e845fba4', 4, '[1,3,2,4]');
INSERT INTO catalog.week_templates VALUES (4, 'urn:musclecuties:seed:v1:29ca941d-8220-4395-b069-18ff11a345a9', 5, '[1,3,6,2,4]');
INSERT INTO catalog.week_templates VALUES (5, 'urn:musclecuties:seed:v1:028ce741-bb69-42db-8dce-2da46e72496b', 6, '[1,3,6,2,4,0]');
INSERT INTO catalog.week_templates VALUES (6, 'urn:musclecuties:seed:v1:8ae6e9ac-4283-4743-b893-76d95d68038a', 7, '[1,3,6,2,4,5,0]');


--
-- Data for Name: workout_planning_config_entries; Type: TABLE DATA; Schema: catalog; Owner: -
--

INSERT INTO catalog.workout_planning_config_entries VALUES (1, 'urn:musclecuties:seed:v1:bfc1dc55-2f17-47bb-b0e7-53a438b4c834', 'readiness', 'sleepLastNight', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (2, 'urn:musclecuties:seed:v1:0f8c4f8b-0f99-451b-8f9a-29bde8a41a04', 'readiness', 'sleep3dAvg', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (3, 'urn:musclecuties:seed:v1:45f94a5e-5089-4019-b7ff-6f7ae9d22d93', 'readiness', 'stepsDeltaPct', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (4, 'urn:musclecuties:seed:v1:8abd1251-4dc5-4764-884d-5e86bfa203fd', 'readiness', 'energyMultiplier', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (5, 'urn:musclecuties:seed:v1:f5002a8c-a60b-463d-bbf1-2546ac97ecd5', 'readiness', 'pain', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (6, 'urn:musclecuties:seed:v1:f6d8a1bd-9798-4041-ab6c-d59225db1322', 'readiness', 'phasePrior', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (7, 'urn:musclecuties:seed:v1:440103d7-5e32-4224-af4d-f2e1cead20c2', 'readiness', 'highTier', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (8, 'urn:musclecuties:seed:v1:8fa1a3e7-faf5-47c0-b792-2d2e5a5f7331', 'readiness', 'moderateTier', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (9, 'urn:musclecuties:seed:v1:bc1c220c-ed9e-4fb3-94eb-2ac525161b47', 'gating', 'hiitMinSleep', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (10, 'urn:musclecuties:seed:v1:293aa25d-6fb2-4544-b027-b63ad77f9e6f', 'gating', 'hiitMaxPain', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (11, 'urn:musclecuties:seed:v1:9ae9fa47-8b63-40f5-9307-4e08660069d9', 'gating', 'hiitMenstrualMinEnergy', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (12, 'urn:musclecuties:seed:v1:7fa71cba-a4c2-4ad8-b4df-2619b41b92e5', 'gating', 'lowReadinessSetMultiplier', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (13, 'urn:musclecuties:seed:v1:5043c972-feb2-4c83-ba80-7d5bb7a8aa2f', 'gating', 'lowReadinessRpeCap', '1');
INSERT INTO catalog.workout_planning_config_entries VALUES (14, 'urn:musclecuties:seed:v1:e4e69486-3793-4a62-bb82-244299407e59', 'gating', 'consecutiveLowDaysToRest', '1');


--
-- Name: exercise_muscle_contributions_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.exercise_muscle_contributions_id_seq', 127, true);


--
-- Name: exercises_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.exercises_id_seq', 104, true);


--
-- Name: food_item_versions_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.food_item_versions_id_seq', 1, false);


--
-- Name: food_items_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.food_items_id_seq', 63, true);


--
-- Name: goal_tier_weights_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.goal_tier_weights_id_seq', 52, true);


--
-- Name: meal_template_entries_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.meal_template_entries_id_seq', 77, true);


--
-- Name: meal_templates_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.meal_templates_id_seq', 17, true);


--
-- Name: quiz_answers_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.quiz_answers_id_seq', 70, true);


--
-- Name: quiz_questions_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.quiz_questions_id_seq', 16, true);


--
-- Name: session_archetypes_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.session_archetypes_id_seq', 7, true);


--
-- Name: slot_templates_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.slot_templates_id_seq', 38, true);


--
-- Name: volume_budget_rows_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.volume_budget_rows_id_seq', 78, true);


--
-- Name: week_templates_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.week_templates_id_seq', 6, true);


--
-- Name: workout_exercise_definitions_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.workout_exercise_definitions_id_seq', 60, true);


--
-- Name: workout_muscle_groups_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.workout_muscle_groups_id_seq', 13, true);


--
-- Name: workout_planning_config_entries_id_seq; Type: SEQUENCE SET; Schema: catalog; Owner: -
--

SELECT pg_catalog.setval('catalog.workout_planning_config_entries_id_seq', 14, true);


--
-- PostgreSQL database dump complete
--

-- Keep identity sequences ahead of the explicit seed ids.
DO $$
BEGIN
    PERFORM setval(pg_get_serial_sequence('catalog.exercise_muscle_contributions', 'id'), COALESCE((SELECT MAX(id) FROM catalog.exercise_muscle_contributions), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.exercises', 'id'), COALESCE((SELECT MAX(id) FROM catalog.exercises), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.food_item_versions', 'id'), COALESCE((SELECT MAX(id) FROM catalog.food_item_versions), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.food_items', 'id'), COALESCE((SELECT MAX(id) FROM catalog.food_items), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.goal_tier_weights', 'id'), COALESCE((SELECT MAX(id) FROM catalog.goal_tier_weights), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.meal_template_entries', 'id'), COALESCE((SELECT MAX(id) FROM catalog.meal_template_entries), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.meal_templates', 'id'), COALESCE((SELECT MAX(id) FROM catalog.meal_templates), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.quiz_answers', 'id'), COALESCE((SELECT MAX(id) FROM catalog.quiz_answers), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.quiz_questions', 'id'), COALESCE((SELECT MAX(id) FROM catalog.quiz_questions), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.session_archetypes', 'id'), COALESCE((SELECT MAX(id) FROM catalog.session_archetypes), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.slot_templates', 'id'), COALESCE((SELECT MAX(id) FROM catalog.slot_templates), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.volume_budget_rows', 'id'), COALESCE((SELECT MAX(id) FROM catalog.volume_budget_rows), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.week_templates', 'id'), COALESCE((SELECT MAX(id) FROM catalog.week_templates), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.workout_exercise_definitions', 'id'), COALESCE((SELECT MAX(id) FROM catalog.workout_exercise_definitions), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.workout_muscle_groups', 'id'), COALESCE((SELECT MAX(id) FROM catalog.workout_muscle_groups), 1));
    PERFORM setval(pg_get_serial_sequence('catalog.workout_planning_config_entries', 'id'), COALESCE((SELECT MAX(id) FROM catalog.workout_planning_config_entries), 1));
END $$;
