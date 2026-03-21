-- phpMyAdmin SQL Dump
-- version 5.2.1
-- https://www.phpmyadmin.net/
--
-- Host: 127.0.0.1
-- Generation Time: Mar 21, 2026 at 06:50 AM
-- Server version: 10.4.32-MariaDB
-- PHP Version: 8.2.12

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `cardsdatabase`
--

-- --------------------------------------------------------

--
-- Table structure for table `cartas`
--

CREATE TABLE `cartas` (
  `ID` int(11) NOT NULL,
  `Nombre` varchar(100) NOT NULL,
  `Tipo` varchar(50) NOT NULL,
  `Costo` int(11) NOT NULL,
  `Ataque` int(11) NOT NULL,
  `Defensa` int(11) NOT NULL,
  `Habilidad` varchar(255) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Dumping data for table `cartas`
--

INSERT INTO `cartas` (`ID`, `Nombre`, `Tipo`, `Costo`, `Ataque`, `Defensa`, `Habilidad`) VALUES
(1, 'Soldado Real', 'Guerrero', 3, 20, 200, 'Ataque de corta distancia con su espada'),
(2, 'Dragón de fuego', 'Aéreo', 6, 45, 300, 'Lanza flamas por su boca'),
(3, 'T-Rex', 'Titán', 0, 80, 500, 'Mordedura colosal que perfora las tropas. (Respawn cada 2 minutos por partidos)'),
(4, 'Sanadora', 'Curandero', 5, 20, 100, 'Cura tropas aliadas'),
(5, 'Granjero', 'Buffer', 0, 0, 999999, 'Cosecha plantas mágicas que sube de nivel a las tropas. (Aparece al inicio de partida)'),
(6, 'Stone Golem', 'Defensivo', 4, 50, 450, 'Solo se dedica a proteger la torre'),
(7, 'Tiburón', 'Defensivo', 5, 30, 250, 'Ataca a tropas enemigos que están en el puente'),
(8, 'Majin', 'Hechicero', 3, 30, 120, 'Lanza flamas de color azul');

-- --------------------------------------------------------

--
-- Table structure for table `__efmigrationshistory`
--

CREATE TABLE `__efmigrationshistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Dumping data for table `__efmigrationshistory`
--

INSERT INTO `__efmigrationshistory` (`MigrationId`, `ProductVersion`) VALUES
('20260321030235_InitialCreate', '8.0.2');

--
-- Indexes for dumped tables
--

--
-- Indexes for table `cartas`
--
ALTER TABLE `cartas`
  ADD PRIMARY KEY (`ID`);

--
-- Indexes for table `__efmigrationshistory`
--
ALTER TABLE `__efmigrationshistory`
  ADD PRIMARY KEY (`MigrationId`);

--
-- AUTO_INCREMENT for dumped tables
--

--
-- AUTO_INCREMENT for table `cartas`
--
ALTER TABLE `cartas`
  MODIFY `ID` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=9;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
