--! \file bootloader_top.vhd
--! \brief top Entit�t des Bootloaders, enth�lt die gesamte Bootloader Funktionalit�t  
--! 
--! Stellt Ports zur direkten Interaktion mit SVNR bereit.
--!	Direkte Interaktion mit FPGA Pins erfolgt durch: 
--! i_rxd
--!

library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
use work.fifo_3_type.all;

--! \brief top Entit�t des Bootloaders, enth�lt die gesamte Bootloader Funktionalit�t  
--! 
--! Stellt Ports zur direkten Interaktion mit SVNR bereit.
--!	Direkte Interaktion mit FPGA Pins erfolgt durch: 
--! i_rxd
--!
entity bootloader_top is
	port (
		i_Clk              : in std_logic;						
		i_rxd              : in std_logic;						--geht in UART_RX							--! Serielle UART RX Daten
		o_txd              : out std_logic;						--kommt vom UART_TX							--! Serielle UART TX Daten

        -- cpu run control
		o_svnr_cpu_en      : out std_logic;						--kommt vom vner_cpu_controller					--! Serielle UART RX Daten
		i_program_counter  : in std_logic_vector(15 downto 0);	--geht in decoder, breakpoint_controller 	--! Program Counter des SVNR
		i_cpu_step_fin     : in std_logic;						--geht in single_step	
		o_svnr_reset	   : out std_logic;						--kommt vom Dicoder	
        -- ram access
		o_svnr_ram_address : out std_logic_vector(15 downto 0);	--
		o_svnr_ram_data : out std_logic_vector(15 downto 0);	--kommt vom runner							--! Wenn Bootloader SVNR-RAM Write Kontrolle hat (bspw. bei Image-Upload): RAM Daten an der Adresse von `o_svnr_ram_address`
		o_svnr_wnr         : out std_logic;						--kommt vom runner							--! Wenn Bootloader SVNR-RAM Read/Write Kontrolle hat: wnr Signal fuer SVNR (Siehe SVNR Dokumentation)
		o_svnr_addrstrb    : out std_logic;						--kommt vom runner							--! Wenn Bootloader SVNR-RAM Read/Write Kontrolle hat (bspw. bei Image-Upload oder Debug RAM Abfrage): Addresse des zuzugreifendem RAM
		i_svnr_ram_data	   : in std_logic_vector(15 downto 0);	--kommt vom runner	
		-- register access
		o_register_data		: out std_logic_vector(15 downto 0);
		o_register_load		: out std_logic;
		o_register_sel		: out std_logic_vector(2 downto 0);
		i_register_data		: in std_logic_vector(15 downto 0)
	);
end bootloader_top;

architecture rtl of bootloader_top is
	--! Want to interface to 115200 baud UART
	--! 50000000 / 115200 = 434.028 Clocks Per Bit.
	constant c_CLKS_PER_BIT : integer := 434;
	constant c_WIDTH : integer := 8;
	constant c_DEPTH : integer := 2;
	constant c_ADDR_WIDTH : natural := 10;
	constant c_DATA_WIDTH : natural := 16;

	--Decoder Ausgänge 
	signal w_flush 				: std_logic := '0'; 					--input: tx_bufer, fifo_3
	signal w_status 			: std_logic_vector(23 downto 0); 		--input: tx_bufer
	signal w_ram_addr 			: std_logic_vector(9 downto 0); 		--input: dpram
	signal w_ram_data 			: std_logic_vector(15 downto 0); 		--input: dpram
	signal w_ram_wen 			: std_logic := '0';						--input: dpram
	signal w_ram_data_valid 	: std_logic := '0'; 					--wird nicht verwendet
	signal w_ram_runner_begin 	: std_logic := '0'; 					--input: runner
	signal w_ram_uploading 		: std_logic; 							--input: tx_buff_controler
	signal r_breakpoint_add 	: std_logic; 							--input: breakpoint_controller
	signal r_breakpoint_delete 	: std_logic; 							--input: breakpoint_controller
	signal s_breakpoint_value 	: std_logic_vector(15 downto 0); 		--input: breakpoint_controller
	signal s_cm_run 			: std_logic; 							--input: breakpoint_controller, single_step, svnr_en_controller und wirdt zu o_svnr_run 
	signal s_tx_trig			: std_logic;						 	--input: tx_buffer
	--Decoder Eingang
	signal r_rst_sync 			: std_logic := '0';						--ist immer 0 wird aber nicht angeschlossen

	--UART_RX Ausgänge
	signal w_RX_DV 	 : std_logic; 						--input: fifo_3 	--! data valid pulse: indicates when RX_Byte has valid content
	signal w_RX_Byte : std_logic_vector(7 downto 0); 	--input: fifo_3 

	--fifo_3 Ausgang
	signal w_RD_DATA 		: std_logic_vector (7 downto 0);		--wird nicht verwendet
	signal w_FULL    		: std_logic := '0'; 					--input: Dicoder 			-- wird als logik für wr_en verwendet
	signal w_rd_data_burst  : std_logic_vector(24 - 1 downto 0); 	--input: Dicoder 			--(3 * 8) - 1
	signal w_EMPTY 			: std_logic := '0'; 					--wird nicht verwendet
	--fifo_3 Eingang
	signal r_RD_EN 			: std_logic := '0'; 					--ist immer 0 wird aber nicht angeschlossen


	--UART_TX Ausgang
	signal w_TX_Active  : std_logic; --wird nicht verwendet
	signal w_TX_Done 	: std_logic; --input: tx_buff_controler

	--runner Ausgang
	signal r_runner_done : std_logic; 														--input: decoder
	signal r_raddr 		 : std_logic_vector(c_ADDR_WIDTH - 1 downto 0) := (others => '0');  --input: dpram

	--tx_buffer Ausgang
	signal w_tx_rdy_to_fetch : std_logic; 						--input: tx_buff_controler
	signal w_data_out 		 : std_logic_vector(23 downto 0);   --input: tx_buff_controler -- Output data

	--dpram Ausgang
	signal w_dout : std_logic_vector(c_DATA_WIDTH - 1 downto 0); --input: runner

	--single_step Ausgang
	signal s_step_en : std_logic; --
	
	--
	signal s_svnr_en : std_logic; --

	--breakpoint_controller Ausgang 
	signal w_bp_controller_cpu_en : std_logic; 		  --input:svnr_en_controller, mux_4_1
	signal r_cpu_halt			  : std_logic; 		  -- not used
	signal r_bp_edit_done 		  : std_logic;		  --input: decoder
	--breakpoint_controller Eingang
	signal s_breakpoint_enable    : std_logic := '0'; --wird nicht verwendet

	--mux_4_1 Ausgang
	signal s_cpu_en_mux_out : std_logic; --wird nicht verwendet

	--tx_buff_controler Ausgang
	signal r_TX_Byte  		: std_logic_vector(7 downto 0); --input: UART_TX
	signal s_tx_packet_done : std_logic := '1';				--input: decoder
	signal r_TX_DV 			: std_logic;					--input: UART_TX
	
	
	signal r_svnr_ram_data_in		: std_logic_vector(15 downto 0);
	signal r_svnr_ram_sel		: std_logic;

	signal o_cm_svnr_ram_address	: std_logic_vector(15 downto 0);
	signal o_cm_svnr_ram_data		: std_logic_vector(15 downto 0);
	signal o_cm_svnr_ram_control	: std_logic_vector(1 downto 0);

	signal o_runner_svnr_ram_address	: std_logic_vector(15 downto 0);
	signal o_runner_svnr_ram_data		: std_logic_vector(15 downto 0);
	signal o_runner_svnr_ram_control	: std_logic_vector(1 downto 0);
	
	signal o_svnr_ram_control	: std_logic_vector(1 downto 0);

begin

	r_rd_en <= '0';
	r_rst_sync <= '0';
	
	o_svnr_cpu_en <= s_svnr_en;


	svnr_ram_control_mux : entity work.mux_2_n
    generic map(
      size => 2
    )
    port map(
      data0 => o_runner_svnr_ram_control,
      data1 => o_cm_svnr_ram_control,
      result => o_svnr_ram_control,
      sel => r_svnr_ram_sel
    );
	o_svnr_addrstrb <= o_svnr_ram_control(0);
	o_svnr_wnr <= o_svnr_ram_control(1);
	
	svnr_ram_addr_mux : entity work.mux_2_n
    generic map(
      size => 16
    )
    port map(
      data0 => o_runner_svnr_ram_address,
      data1 => o_cm_svnr_ram_address,
      result => o_svnr_ram_address,
      sel => r_svnr_ram_sel
    );
	
	svnr_ram_data_mux : entity work.mux_2_n
    generic map(
      size => 16
    )
    port map(
      data0 => o_runner_svnr_ram_data,
      data1 => o_cm_svnr_ram_data,
      result => o_svnr_ram_data,
      sel => r_svnr_ram_sel
    );

	r_svnr_ram_data_in <= i_svnr_ram_data;



	--! UART RX Komponente
	UART_RX_INST : entity work.UART_RX
		generic map(
			g_CLKS_PER_BIT => c_CLKS_PER_BIT
		)
		port map(
			--Input
			i_Clk         => i_Clk
			, i_RX_Serial => i_rxd
			--Output
			, o_RX_DV     => w_RX_DV
			, o_RX_Byte   => w_RX_Byte
		);


	--! UART RX Buffer
	FIFO_INST : entity work.fifo_3
		port map(
			--Input
			i_rst_sync 		=> w_flush, 		-- wird als flush verwendet
			i_clk     	    => i_Clk,
			i_wr_en   		=> w_RX_DV,
			i_wr_data 		=> w_RX_Byte,
			i_rd_en         => r_RD_EN,   		-- wird nicht verwendet
			--Output
			o_full    		=> w_FULL, 			-- wird als logik für wr_en verwendet
			o_rd_data       => w_RD_DATA, 		-- wird nicht verwendet
			o_empty         => w_EMPTY,   		-- wird nicht verwendet
			o_rd_data_burst => w_rd_data_burst
		);



	--! Decoder f�r Bootloader Pakete - enth�lt die Grundlogik des Bootloaders
	decoder_inst : entity work.decoder
		port map(
			--Input
			i_rst_sync 				=> r_rst_sync
			, i_clk   				=> i_Clk
			
			, o_tx_trig			  	=> s_tx_trig
			, o_tx_data            	=> w_status

			, i_rx_data		 	  	=> w_rd_data_burst
			, i_rx_full        	  	=> w_FULL
			, o_flush             	=> w_flush

			-- bootloader
			, i_runner_done  	  	=> r_runner_done
			, o_ram_addr          	=> w_ram_addr
			, o_ram_data          	=> w_ram_data
			, o_ram_wen           	=> w_ram_wen
			, o_ram_runner_begin  	=> w_ram_runner_begin
			-- cpu controller
			, i_bp_edit_done  	  	=> r_bp_edit_done
			, o_breakpoint_add    	=> r_breakpoint_add
			, o_breakpoint_delete 	=> r_breakpoint_delete
			, o_breakpoint_value  	=> s_breakpoint_value
			
			, i_svnr_running 	  	=> s_svnr_en ---------------
			, o_svnr_run           	=> s_cm_run
			, o_step_en				=> s_step_en ----------------------


			, i_program_counter	  	=> i_program_counter
			, o_svnr_reset		  	=> o_svnr_reset
			
			, o_svnr_ram_data 		=> o_cm_svnr_ram_data
			, o_svnr_ram_addr 		=> o_cm_svnr_ram_address
			, o_svnr_ram_wnr 		=> o_cm_svnr_ram_control(1)
			, o_svnr_ram_addrstrb 	=> o_cm_svnr_ram_control(0)
			, o_svnr_ram_sel 		=> r_svnr_ram_sel
			, i_svnr_ram_data 		=> r_svnr_ram_data_in
			
			-- register access
			,o_register_data		=> o_register_data
			,o_register_load		=> o_register_load
			,o_register_sel			=> o_register_sel
			,i_register_data		=> i_register_data


			-- not used
			, i_tx_done			  	=> s_tx_packet_done
			, o_ram_data_valid    	=> w_ram_data_valid
			, o_ram_uploading	  	=> w_ram_uploading
		);


	runner_inst: entity work.runner
		port map(
			--Input
			i_Clk				=> i_Clk,
			i_Begin				=> w_ram_runner_begin,
			o_Done				=> r_runner_done,
			--Output
			i_dpram_data		=> w_dout,
			o_dpram_raddr		=> r_raddr, 			--same as o_svnr_ram_address
			o_svnr_ram_address	=> o_runner_svnr_ram_address, 	--same as o_dpram_raddr
			o_svnr_ram_data_in	=> o_runner_svnr_ram_data, 	--same as i_dpram_data
			o_svnr_wnr			=> o_runner_svnr_ram_control(1),
			o_svnr_addrstrb		=> o_runner_svnr_ram_control(0)
		);


	tx_buffer_inst : entity work.tx_buffer
		port map(
			--Input
			clk            => i_Clk,
			i_flush		   => w_flush,
			i_tx_trig	   => s_tx_trig,
			data_in        => w_status,
			--Output
			data_out       => w_data_out,
			o_rdy_to_fetch => w_tx_rdy_to_fetch
		);


	UART_TX_inst : entity work.UART_TX
		generic map(
			-- 50000000 / 115200 = 434.028 Clocks Per Bit.
			g_CLKS_PER_BIT => c_CLKS_PER_BIT -- Needs to be set correctly
		)
		port map(
			--Input
			i_Clk         => i_Clk
			, i_TX_DV     => r_TX_DV
			, i_TX_Byte   => r_TX_Byte
			--Output
			, o_TX_Active => w_TX_Active
			, o_TX_Serial => o_txd
			, o_TX_Done   => w_TX_Done
		);
	--port map ( r_Clk,r_TX_DV,r_TX_Byte,w_TX_Active,w_TX_Serial,w_TX_Done);

	tx_buff_controler: entity work.tx_buff_controler
		port map(
			--Input
			i_Clk              =>  i_Clk, 
			i_ram_uploading    =>  w_ram_uploading,
			i_tx_rdy_to_fetch  =>  w_tx_rdy_to_fetch,
			i_TX_Done          =>  w_TX_Done,
			i_data_out         =>  w_data_out, 
			--Output
			o_TX_Byte           => r_TX_Byte,
			o_tx_packet_done    => s_tx_packet_done,
			o_Tx_DV             => r_Tx_DV
		);
	
    
    -- clk überprüfen da sie zwei mal in das modul geht 
	-- DPRAM
	dpram_inst : entity work.dpram
		generic map(
			addr_width   => c_ADDR_WIDTH --1024 speicher adressen 
			, data_width => c_DATA_WIDTH -- 2 Byte pro adresse
		)
		port map(
			--Input
			write_en =>  w_ram_wen
			, waddr  => w_ram_addr
			, wclk   => i_clk
			, din    => w_ram_data
			, raddr  => r_raddr
			, rclk   => i_clk
			--Output
			, dout   => w_dout
		);


	breakpoint_controller_inst : entity work.breakpoint_controller
		port map(
			--Input
			i_clk               => i_clk,
			i_program_counter   => i_program_counter,
			i_breakpoint_delete => r_breakpoint_delete,
			i_breakpoint_add    => r_breakpoint_add,
			i_breakpoint_value  => s_breakpoint_value,
			i_breakpoint_enable => s_breakpoint_enable,
			i_run               => s_cm_run,
			--Output
			o_cpu_en            => w_bp_controller_cpu_en,
			o_cpu_halt			=> r_cpu_halt,	-- not used
			o_edit_done         => r_bp_edit_done
		);


	-- single_step_inst : entity work.single_step
	-- 	port map(
	-- 		--Input
	-- 		i_run          => s_cm_run,
	-- 		i_cpu_step_fin => i_cpu_step_fin,
	-- 		--Output
	-- 		o_cpu_en       => w_single_step_cpu_en
	-- 	);


		svnr_en_controller : entity work.svnr_en_controller
			port map(
				i_Clk           	=>  i_Clk,
				i_cm_run           	=>  s_cm_run,
				i_bp_run           	=>  w_bp_controller_cpu_en,
				i_single_step_en    =>  s_step_en,
				i_step_finished     =>  i_cpu_step_fin,
				o_svnr_cpu_en       =>  s_svnr_en
			);
		
		-- svnr_en_controller : entity work.svnr_en_controller
		-- 	port map(
		-- 		--Input
		-- 		i_Clk               => i_Clk,
		-- 		i_cpu_run           => s_cm_run,
		-- 		i_bp_controller_cpu_en    => w_bp_controller_cpu_en,
		-- 		--Output
		-- 		s_svnr_cpu_en       => s_svnr_en
		-- 	);

	-- Schaltet zwischen den Erzeugern des cpu_en Signal. Da im Moment nur Breakpoints die CPU steuern k�nnen, ist diese Verbindung hardwired. 
	--die Komponente lag zunächst zwischen Breakpointcontroller und snver_cpu_controller. Jetzt ist ihr Ausgang nirgends angeschlossen.
	--Für eventuelle Erweiterung haben wir sie im ruhendem Zustand im Toplevel gelassen.
	-- cpu_en_mux : entity work.mux_4_1
	-- 	port map(
	-- 		a               => w_bp_controller_cpu_en,
	-- 		b               => w_single_step_cpu_en, -- TODO: UPLOAD RAM Mode?
	-- 		c               => '1',	
	-- 		d               => '0',
	-- 		y               => s_cpu_en_mux_out,
	-- 		sel(1 downto 0) => "00"
	-- 	);

end rtl;