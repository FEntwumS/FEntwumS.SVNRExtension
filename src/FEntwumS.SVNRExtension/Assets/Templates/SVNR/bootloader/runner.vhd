--! \file runner.vhd
--! \brief State-Machine, die nach F�llen des DPRAMs den gesamten Inhalt sukzessiv in den SVNR RAM kopiert 
--! 
--! L�uft iterativ �ber den Inhalt des gesamten DPRAMs, und schreibt den derzeitigen Inhalt in die ensprechende Addresse des SVNR Rams. 
--! Hierbei steuert der Runner das Setzen der erforderlichen Signale um auf den RAM zuzugreifen. Dabei folgt der Runner genau dem Timing des SVNR bei RAM-Zugriff.
--!

LIBRARY IEEE;
USE IEEE.STD_LOGIC_1164.ALL;
USE IEEE.NUMERIC_STD.ALL;

--! \brief State-Machine, die nach F�llen des DPRAMs den gesamten Inhalt sukzessiv in den SVNR RAM kopiert 
--! 
--! L�uft iterativ �ber den Inhalt des gesamten DPRAMs, und schreibt den derzeitigen Inhalt in die ensprechende Addresse des SVNR Rams. 
--! Hierbei steuert der Runner das Setzen der erforderlichen Signale um auf den RAM zuzugreifen. Dabei folgt der Runner genau dem Timing des SVNR bei RAM-Zugriff.
--!
ENTITY runner IS
	port (
		i_Clk				: in std_logic;
		i_Begin				: in std_logic;							--! Starte Kopieren von DPRAM in SVNR RAM
		o_Done				: out std_logic;						--! Kopieren on DPRAM in SVNR RAM erfolgreich abgeschlossen
		i_dpram_data		: in std_logic_vector(15 downto 0);		--! Derzeitige DPRAM Daten an Adresse `o_dpram_raddr`
		o_dpram_raddr		: out std_logic_vector(9 downto 0); 	--! Gleich wie `o_svnr_ram_address`
    	o_svnr_ram_address	: out std_logic_vector(15 downto 0); 	--! Gleich wie `o_dpram_raddr`
    	o_svnr_ram_data_in	: out std_logic_vector(15 downto 0); 	--! Gleich wie `i_dpram_data`
    	o_svnr_wnr			: out std_logic;						--! SVNR wnr Signal zur RAM Kontrolle (Siehe SVNR Dokumentation) 
    	o_svnr_addrstrb		: out std_logic							--! SVNR addrstrb Signal zur RAM Kontrolle (Siehe SVNR Dokumentation) 
	);
end runner;

ARCHITECTURE rtl OF runner IS
	--  CONSTANT c_ramsize : NATURAL := 28 - 1;         
    -- CONSTANT c_ramsize : NATURAL := 128 - 1;         
    CONSTANT c_ramsize : NATURAL := 1024 - 1;         

	SIGNAL r_cnt : NATURAL RANGE 0 TO c_ramsize :=0;
    TYPE STATE_TYPE IS ( s_INIT
                        ,s_RUNNING
                        ,s_NEXT
                        ,s_CLEAR
						);
    SIGNAL state : STATE_TYPE := s_INIT;           

BEGIN

	o_svnr_ram_data_in <= i_dpram_data; -- daten von dpram wird durchgeschliffen

	p_fsm : PROCESS(i_Clk) 
	BEGIN
	IF rising_edge(i_Clk) THEN
		CASE state IS
			WHEN s_INIT =>	
				IF i_Begin = '1' THEN
					r_cnt			<= 0;
					o_Done			<= '0';
					o_svnr_wnr		<= '1';
					o_svnr_addrstrb	<= '0';
					state <= s_RUNNING;
				END IF;
			WHEN s_RUNNING =>	
				o_svnr_addrstrb	<= '1';
				state <= s_NEXT;
			WHEN s_NEXT =>	
				IF r_cnt < c_ramsize THEN 
					r_cnt 			<= r_cnt + 1;
					o_svnr_addrstrb	<= '0';
					state 			<= s_RUNNING;
				ELSIF r_cnt = c_ramsize THEN
					r_cnt			<= 0;
					o_svnr_addrstrb	<= '0';
					o_Done			<= '1';
					o_svnr_wnr		<= '0';
					state			<= s_CLEAR;
				END IF;
			WHEN s_CLEAR =>
				r_cnt			<= 0;
				o_svnr_addrstrb	<= '0';
				o_Done			<= '0';
				o_svnr_wnr		<= '0';
				state			<= s_INIT;
			WHEN OTHERS =>	
				state			<= s_CLEAR;
		END CASE;
	END IF;
	END PROCESS;

	o_dpram_raddr		<= std_logic_vector(to_unsigned(r_cnt,o_dpram_raddr'length)); 
   	o_svnr_ram_address	<= std_logic_vector(to_unsigned(r_cnt,o_svnr_ram_address'length));

END ARCHITECTURE;
