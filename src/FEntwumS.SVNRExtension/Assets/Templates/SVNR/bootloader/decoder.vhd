--! \file decoder.vhd
--! \brief Verarbeitet SBDP-2 Pakete und steuert den SVNR entsprechend. 
--! 

library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
use work.fifo_3_type.all;

entity decoder is
    port (
		-- clock and reset
        i_rst_sync : in std_logic;
        i_clk      : in std_logic;

        i_rx_data		   	: in std_logic_vector((c_WIDTH  * c_DEPTH ) - 1 downto 0 ); 	--! 3 Byte - 1 Bootloader Paket Input aus dem FIFO RX Input Buffer
        i_rx_full			: in std_logic;													--! Read Signal 
		i_bp_edit_done 		: in std_logic;													--! Signalisiert Breakpoint Hinzufuegen/Loeschen abgeschlossen
		i_svnr_running		: in std_logic;													--! Signalisiert CPU ist angehalten
		i_program_counter	: in std_logic_vector(15 downto 0);								--! Indiziert die Adresse, an der die CPU angehalten hat. 
		i_tx_done			: in std_logic;													--! Signalisiert ein gesamtes SBDP-2 Paket (3 Byte) per UART uebertragen
        o_flush 			: out std_logic;												--! Bootloader Paket ist erfolgreich gelesen 
		o_tx_data			: out std_logic_vector(23 downto 0) := X"000001"; 				--! Status/Response Paket, welches von UART_tx abgegriffen und bei Statusabfrage versendet wird.
		o_tx_trig			: out std_logic := '0';											--! Startet UART TX Response Paketuebertragung
		
		-- output RAM Interface
		i_runner_done		: in std_logic;													--! Signalisiert Kopieren von DPRAM in den SVNR RAM erfolgreich abgeschlossen - Wenn das Signal high ist, ist die RAM Uebertragung vollstaendig abgeschlossen
		o_ram_addr			: out std_logic_vector(9 downto 0);								--! 
		o_ram_data			: out std_logic_vector(15 downto 0);							--! 
		o_ram_wen			: out std_logic;												--! 
		o_ram_runner_begin	: out std_logic := '0';											--! Startet das Kopieren von DPRAM in den SVNR RAM durch den Runner

		i_svnr_ram_data		: in std_logic_vector(15 downto 0);
		o_svnr_ram_data		: out std_logic_vector(15 downto 0);
		o_svnr_ram_addr		: out std_logic_vector(15 downto 0);
		o_svnr_ram_wnr		: out std_logic;
		o_svnr_ram_addrstrb	: out std_logic;
		
		o_svnr_ram_sel		: out std_logic;

		-- output breakpoint interface
		o_breakpoint_add	: out std_logic := '0';											--! Signalisiert Hinzufuegen eines Breakpoints an den Breakpoint-Controller
		o_breakpoint_delete	: out std_logic := '0';											--! Signalisiert Loeschen eines Breakpoints an den Breakpoint-Controller
		o_breakpoint_value 	: out std_logic_vector(15 downto 0) := X"FFFF";					--! Adresse des Hinzuzufuegenden/ zu Loeschenden Breakpoints 

		o_step_en			: out std_logic := '0';											--! 
		o_svnr_run			: out std_logic := '0';											--! 
		o_svnr_reset		: out std_logic := '0';											--! SVNR Reset

		-- register access
		o_register_data		: out std_logic_vector(15 downto 0);
		o_register_load		: out std_logic;
		o_register_sel		: out std_logic_vector(2 downto 0);
		i_register_data		: in std_logic_vector(15 downto 0)
		);
end decoder;

architecture rtl of decoder is
	CONSTANT c_RAM_SIZE : NATURAL := 1024;
    signal s_rx_data : std_logic_vector((c_WIDTH * c_DEPTH ) - 1 downto 0 );

	type STATE is (
		 z_POWER_ON				--! initialisierungs zustand
		,z_UPLOAD_TO_RAM 		--!
		,z_WRITE_TO_RAM 		--! 
		,z_RAM_FULL 			--! 
		,z_RAM_NOT_FULL 		--! 
		,z_RAM_OVERFLOW  		--! 
		,z_RAM_FULL_OK   		--! 
		,z_DEBUG_INIT   		--! 
		,z_TO_DEBUG_INIT
		,z_ADDING_BP
		,z_BP_ADDED
		,z_DELETING_BP
		,z_BP_DELETED
		,z_DEBUG_START_RUNNING
		,z_DEBUG_RUNNING  		--! 
		,z_DEBUG_HALT			--! 
		,z_DEBUG_START_STEP
		,z_DEBUG_STEP			--! 
		,z_RUNNING  			--! 
		,z_RESETTING  			--! 
		,z_TO_POWER_ON
		,z_AWAIT_RAM_WRITE_DATA
		,z_WAIT_RAM_READ_DATA
		,z_RAM_WRITE
		,z_SEND_RAM_READ_DATA
		,z_SEND_REG_DATA
		,z_AWAIT_REG_DATA
		,z_REG_WRITE
	);
  	signal fsm_state_decoder: STATE := z_POWER_ON;
	signal ram_cnt 			: NATURAL := 0;
	signal ram_output_data 	: std_logic_vector(15 downto 0) := X"0000";
	signal data_address 	: std_logic_vector (9 downto 0);
	signal write_enable		: std_logic := '0';
	signal b_full			: std_logic := '0';
	signal s_tx_data			: std_logic_vector(23 downto 0) := X"000000"; 
	signal b_new_data		: std_logic := '0';
	signal w_done			: std_logic := '0';

begin

    p_CONTROL : process (i_clk) is
    begin
        if rising_edge(i_clk) then
            if i_rst_sync = '1' then
				s_rx_data <= (others => '0');
				o_flush <= '0';
				b_new_data <= '0';
            elsif i_rx_full = '1' then
				s_rx_data <= i_rx_data;
				b_new_data <= '1';
				o_flush <= '1';
			else
				o_flush <= '0';
				b_new_data <= '0';
            end if; -- sync reset
        end if; -- rising_edge(i_clk)
    end process p_CONTROL;

	o_tx_data <=  s_tx_data;
	-- Automat
	p_automat : PROCESS(i_Clk) 
	BEGIN 
	IF rising_edge(i_Clk) THEN 
			CASE fsm_state_decoder is
				WHEN z_POWER_ON	=> 		-- initialisierungs zustand
					o_svnr_ram_data 	<= X"0000";
					o_svnr_ram_addr 	<= X"0000";
					o_svnr_ram_wnr		<= '0';
					o_svnr_ram_addrstrb	<= '0';					
					o_svnr_ram_sel		<= '0';
					
					o_register_data <= X"0000";
					o_register_load <= '0';
					o_register_sel <= "000";
					
					o_svnr_reset <= '0';
					o_tx_trig <= '0';
					o_svnr_run <= '0';
					IF s_rx_data = X"010001" AND b_new_data = '1' THEN  -- EXECUTE SVNR
						o_svnr_run <= '1';
						fsm_state_decoder <= z_RUNNING;
						ram_cnt <= 0;
					END IF;
					IF s_rx_data = X"010002" AND b_new_data = '1' THEN  -- Jump into RAM upload mode
						o_ram_runner_begin <= '0';
						fsm_state_decoder <= z_UPLOAD_TO_RAM;
						ram_cnt <= 0;
					END IF;
					IF s_rx_data = X"010006" AND b_new_data = '1' THEN  -- Jump into debug mode
						fsm_state_decoder <= z_DEBUG_INIT;
					END IF;
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN	-- Statusabfrage
							s_tx_data <= X"000001"; 
							o_tx_trig <= '1';
					END IF;
					IF s_rx_data = X"010007" AND b_new_data = '1' THEN  -- reset svnr
						o_svnr_reset <= '1';
						fsm_state_decoder <= z_RESETTING;
					END IF;

				WHEN z_UPLOAD_TO_RAM => 
					o_tx_trig <= '0';
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN
						s_tx_data <= X"030011";
						o_tx_trig <= '1';
						fsm_state_decoder <= z_RAM_NOT_FULL;
					ELSIF s_rx_data(23 downto 16) = X"02" AND b_new_data = '1' THEN
						IF ram_cnt = c_RAM_SIZE - 1 THEN 
							b_full <= '1';
						ELSE 
							b_full <= '0';
						END IF;

						write_enable <= '1';
						ram_output_data <= s_rx_data(15 downto 0);
						fsm_state_decoder <= z_WRITE_TO_RAM;
					END IF;

				WHEN z_WRITE_TO_RAM => 
					write_enable <= '0';
					IF b_full = '1' THEN 
						fsm_state_decoder <= z_RAM_FULL;
					ELSE
						ram_cnt 	<= ram_cnt + 1;
						fsm_state_decoder 	<= z_UPLOAD_TO_RAM;
						
					END IF;
				WHEN z_RAM_FULL =>	
					IF s_rx_data(23 downto 16) = X"02" AND b_new_data = '1' THEN
						ram_cnt		<= 0; 
						fsm_state_decoder 	<= z_RAM_OVERFLOW;

					END IF;	
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN
						ram_cnt		<= 0; 
						o_ram_runner_begin <= '1';
						s_tx_data <=  X"000003"; 
						o_tx_trig <= '1';
						fsm_state_decoder 	<= z_RAM_FULL_OK;

					END IF;	
				WHEN z_RAM_NOT_FULL =>	
					o_tx_trig <= '0';
					ram_cnt		<= 0; --
					fsm_state_decoder 	<= z_POWER_ON;

				WHEN z_RAM_OVERFLOW => 
					o_tx_trig <= '0';
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN 
						ram_cnt		<= 0;
						s_tx_data <=  X"030012"; 
						o_tx_trig <= '1';
						fsm_state_decoder 	<= z_TO_POWER_ON;
					END IF;
				WHEN z_TO_POWER_ON => 	
					fsm_state_decoder 	<= z_POWER_ON;

				WHEN z_RAM_FULL_OK =>
					o_tx_trig <= '0';
					if i_runner_done = '1' then
						o_ram_runner_begin <= '0';
						fsm_state_decoder 	<= z_POWER_ON;
					end if;
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN
						s_tx_data <=  X"000003"; 
						o_tx_trig <= '1';
					END IF;




				WHEN z_DEBUG_INIT => 

					o_tx_trig			<= '0';

					o_breakpoint_add 	<= '0';
					o_breakpoint_delete <= '0';
					o_step_en		 	<= '0';

					o_svnr_reset		<= '0';
					o_svnr_run 			<= '0';

					o_svnr_ram_data 	<= X"0000";
					o_svnr_ram_addr 	<= X"0000";
					o_svnr_ram_wnr		<= '0';
					o_svnr_ram_addrstrb	<= '0';					
					o_svnr_ram_sel		<= '0';
					
					o_register_data <= X"0000";
					o_register_load <= '0';
					o_register_sel <= "000";

					IF s_rx_data = X"010005" AND b_new_data = '1' THEN
						s_tx_data <= X"000005";	-- 
						o_tx_trig <= '1';
					END IF;
					
					IF s_rx_data = X"010004" AND b_new_data = '1' THEN  -- switch power_on
						fsm_state_decoder <= z_POWER_ON;
					END IF;
							
					IF s_rx_data = X"010007" AND b_new_data = '1' THEN  -- reset svnr
						o_svnr_reset <= '1';
					END IF;

					IF s_rx_data = X"010001" AND b_new_data = '1' THEN  -- start running
						o_svnr_run <= '1';
						fsm_state_decoder <= z_DEBUG_START_RUNNING;
					END IF;

					IF s_rx_data = X"010009" AND b_new_data = '1' THEN  -- step
						o_svnr_run <= '1';
						fsm_state_decoder <= z_DEBUG_START_STEP;
					END IF;
					
					IF s_rx_data = X"010200" AND b_new_data = '1' THEN
					 	-- s_tx_data(23 downto 16) <= X"06";
					 	-- s_tx_data(15 downto 0) <= i_program_counter;
						-- o_tx_trig <= '1';
						
						o_register_sel <= "011";
						fsm_state_decoder <= z_SEND_REG_DATA;
					END IF;


					
					IF s_rx_data = X"010201" AND b_new_data = '1' THEN  -- Command: read Befehls-Register
						o_register_sel <= "001";
						fsm_state_decoder <= z_SEND_REG_DATA;
					END IF;
					IF s_rx_data = X"010202" AND b_new_data = '1' THEN  -- Command: read Akku
						o_register_sel <= "000";
						fsm_state_decoder <= z_SEND_REG_DATA;
					END IF;
					IF s_rx_data = X"010203" AND b_new_data = '1' THEN  -- Command: read Hilfs-Register
						o_register_sel <= "010";
						fsm_state_decoder <= z_SEND_REG_DATA;
					END IF;
					IF s_rx_data = X"010204" AND b_new_data = '1' THEN  -- Command: read ALU status
						o_register_sel <= "100";
						fsm_state_decoder <= z_SEND_REG_DATA;
					END IF;
					
					IF s_rx_data = X"010205" AND b_new_data = '1' THEN  -- Command: write Program-Counter
						o_register_sel <= "011";
						fsm_state_decoder <= z_AWAIT_REG_DATA;
					END IF;
					IF s_rx_data = X"010206" AND b_new_data = '1' THEN  -- Command: write Befehls-Register
						o_register_sel <= "001";
						fsm_state_decoder <= z_AWAIT_REG_DATA;
					END IF;
					IF s_rx_data = X"010207" AND b_new_data = '1' THEN  -- Command: write Akku
						o_register_sel <= "000";
						fsm_state_decoder <= z_AWAIT_REG_DATA;
					END IF;
					IF s_rx_data = X"010208" AND b_new_data = '1' THEN  -- Command: write Hilfs-Register
						o_register_sel <= "010";
						fsm_state_decoder <= z_AWAIT_REG_DATA;
					END IF;



					IF s_rx_data(23 downto 16) = X"04" AND b_new_data = '1' THEN  -- SET BREAKPOINT Comand
						o_breakpoint_value <= s_rx_data(15 downto 0);  		
						o_breakpoint_add <= '1';
						fsm_state_decoder <= z_ADDING_BP;
					END IF;

					IF s_rx_data(23 downto 16) = X"05" AND b_new_data = '1' THEN  -- DEL BREAKPOINT Comand
						o_breakpoint_value <= s_rx_data(15 downto 0);  		
						o_breakpoint_delete <= '1';
						fsm_state_decoder <= z_DELETING_BP;
					END IF;
					


					
					IF s_rx_data(23 downto 16) = X"08" AND b_new_data = '1' THEN  -- 
						o_svnr_ram_addr <= s_rx_data(15 downto 0);
						o_svnr_ram_addrstrb	<= '0';					
						fsm_state_decoder <= z_AWAIT_RAM_WRITE_DATA;
					END IF;
					
					IF s_rx_data(23 downto 16) = X"07" AND b_new_data = '1' THEN  -- 
						o_svnr_ram_addr <= s_rx_data(15 downto 0);
						o_svnr_ram_sel		<= '1';
						fsm_state_decoder <= z_WAIT_RAM_READ_DATA;
					END IF;
				
				WHEN z_AWAIT_RAM_WRITE_DATA => 
					IF s_rx_data(23 downto 16) = X"09" AND b_new_data = '1' THEN  -- 
						o_svnr_ram_data <= s_rx_data(15 downto 0);
						o_svnr_ram_wnr		<= '1';
						o_svnr_ram_sel		<= '1';
						fsm_state_decoder <= z_RAM_WRITE;
					END IF;
					
				WHEN z_RAM_WRITE => 
					o_svnr_ram_addrstrb	<= '1';					
					fsm_state_decoder <= z_DEBUG_INIT;

					
				WHEN z_WAIT_RAM_READ_DATA => 
						fsm_state_decoder <= z_SEND_RAM_READ_DATA;

				WHEN z_SEND_RAM_READ_DATA => 
					o_svnr_ram_sel		<= '0';
					o_tx_trig <= '1';
					s_tx_data(23 downto 16) <= X"09";
					s_tx_data(15 downto 0) <= i_svnr_ram_data;
					fsm_state_decoder <= z_DEBUG_INIT;





				WHEN z_SEND_REG_DATA =>	
					s_tx_data(23 downto 16) <= X"06";
					s_tx_data(15 downto 0) <= i_register_data;

					o_tx_trig <= '1';
					
					fsm_state_decoder <= z_DEBUG_INIT;

				WHEN z_AWAIT_REG_DATA => 
					IF s_rx_data(23 downto 16) = X"06" AND b_new_data = '1' THEN  -- 
						o_register_data <= s_rx_data(15 downto 0);
						fsm_state_decoder <= z_REG_WRITE;
					END IF;
				WHEN z_REG_WRITE => 
					o_register_load <= '1';
					fsm_state_decoder <= z_DEBUG_INIT;





				WHEN z_DEBUG_START_RUNNING => 
					fsm_state_decoder <= z_DEBUG_RUNNING;

				WHEN z_DEBUG_RUNNING => 
					o_tx_trig <= '0';					
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN
						o_tx_trig <= '1';
						s_tx_data <= X"000006"; 
					END IF;
					IF i_svnr_running = '0' or (s_rx_data = X"010008" AND b_new_data = '1') THEN
						o_svnr_run <= '0';
						fsm_state_decoder <= z_DEBUG_HALT;
					END IF;

				WHEN z_DEBUG_START_STEP =>
					fsm_state_decoder <= z_DEBUG_STEP;

				WHEN z_DEBUG_STEP =>
					o_step_en <= '1';
					IF i_svnr_running = '0' THEN
						o_svnr_run <= '0';
						fsm_state_decoder <= z_DEBUG_HALT;
					END IF;


				WHEN z_DEBUG_HALT => 
					o_step_en <= '0';
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN  -- 
						o_tx_trig <= '1';
						s_tx_data <= X"00000a"; 
						fsm_state_decoder <= z_TO_DEBUG_INIT;
					END IF;




				WHEN z_ADDING_BP => 
					o_tx_trig <= '0';
					o_breakpoint_add <= '0';
					
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN
						o_tx_trig <= '1';
						s_tx_data <= X"030015"; 
						fsm_state_decoder <= z_TO_DEBUG_INIT;
					END IF;

					IF i_bp_edit_done = '1' THEN
						fsm_state_decoder <= z_BP_ADDED;
					END IF;

				WHEN z_DELETING_BP => 
					o_tx_trig <= '0';
					o_breakpoint_delete <= '0';
					
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN
						o_tx_trig <= '1';
						s_tx_data <= X"030016";  
						fsm_state_decoder <= z_TO_DEBUG_INIT;
					END IF;
					
					IF i_bp_edit_done = '1' THEN
						fsm_state_decoder <= z_BP_DELETED;
					END IF;


				WHEN z_BP_ADDED => 
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN
						o_tx_trig <= '1';
						s_tx_data <= X"00000c";
						fsm_state_decoder <= z_TO_DEBUG_INIT;
					END IF;

				WHEN z_BP_DELETED => 
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN
						o_tx_trig <= '1';
						s_tx_data <= X"00000d";
						fsm_state_decoder <= z_TO_DEBUG_INIT;
					END IF;

				WHEN z_TO_DEBUG_INIT => 
					o_tx_trig <= '0';
					fsm_state_decoder <= z_DEBUG_INIT;
					


				WHEN z_RUNNING => 
					o_tx_trig <= '0';
					IF s_rx_data = X"010005" AND b_new_data = '1' THEN
							s_tx_data <= X"000004"; 
							o_tx_trig <= '1';
					END IF;
					IF s_rx_data = X"010007" AND b_new_data = '1' THEN  -- reset svnr
						o_svnr_reset <= '1';
						o_svnr_run <= '0';
						fsm_state_decoder <= z_RESETTING;
					END IF;					
				
				WHEN z_RESETTING =>
					o_svnr_reset <= '0';
					fsm_state_decoder <= z_POWER_ON;

				WHEN OTHERS =>
					fsm_state_decoder <= z_POWER_ON;
			END CASE;
	END IF;
	END PROCESS;

	o_ram_addr 	<= std_logic_vector(to_unsigned(ram_cnt,o_ram_addr'length));
	o_ram_data 	<= ram_output_data;
	o_ram_wen 	<= write_enable;

end rtl;
